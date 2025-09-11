using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class BarberStoreManager(IBarberStoreDal barberStoreDal, IWorkingHourDal workingHourDal, IManuelBarberDal _manuelBarberDal, ISlotService slotService, IBarberStoreChairDal _barberStoreChairDal, IServiceOfferingDal _serviceOfferingDal,IAppointmentDal appointmentDal, IMapper _mapper) : IBarberStoreService
    {
        [ValidationAspect(typeof(BarberStoreCreateDtoValidator))]
        public async Task<IResult> Add(BarberStoreCreateDto dto, Guid currentUserId)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var store = dto.Adapt<BarberStore>();
            store.BarberStoreUserId = currentUserId;
            await barberStoreDal.Add(store);
            var whList = dto.WorkingHours.Select(r => new WorkingHour
            {
                OwnerId = store.Id,
                DayOfWeek = r.DayOfWeek,
                StartTime = r.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(r.StartTime),
                EndTime = r.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(r.EndTime),
                IsClosed = r.IsClosed
            }).ToList();
            await workingHourDal.AddRange(whList);
            var newMbDict = new Dictionary<Guid, ManuelBarber>();
            if (dto.ManualBarbers?.Any() == true)
            {
                var newEntities = dto.ManualBarbers.Select(x =>
                {
                    var mb = x.Adapt<ManuelBarber>();
                    mb.StoreId = store.Id;                
                    return mb;
                }).ToList();
                await _manuelBarberDal.AddRange(newEntities);
                foreach (var ent in newEntities)
                    newMbDict[ent.TempId] = ent;         
            }
            var chairEntities = dto.Chairs?.Select(chDto =>
            {
                var ch = chDto.Adapt<BarberChair>();
                ch.StoreId = store.Id;
                if (chDto.Type == ChairMode.Name)
                {
                    ch.Name = chDto.Name;
                    ch.ManualBarberId = null;
                }
                else
                {
                    if (chDto.ManualBarberTempId.HasValue && newMbDict.TryGetValue(chDto.ManualBarberTempId.Value, out var mb))
                    {
                        ch.ManualBarberId = mb.Id;
                    }
                    ch.Name = null;
                }
                return ch;
            }).ToList() ?? new();
            if (chairEntities.Any())
                await _barberStoreChairDal.AddRange(chairEntities);

            var offerEntities = dto.Offerings?.Select(o =>
            {
                var e = o.Adapt<ServiceOffering>();
                e.OwnerId = store.Id;
                return e;
            }).ToList() ?? new();
            if (offerEntities.Any())
                await _serviceOfferingDal.AddRange(offerEntities);
            scope.Complete();
            return new SuccessResult("Berber dükkanı başarıyla oluşturuldu.");
        }

        [ValidationAspect(typeof(BarberStoreUpdateDtoValidator))]
        public async Task<IResult> Update(BarberStoreUpdateDto dto)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var store = await barberStoreDal.Get(x => x.Id == dto.Id);
            if (store == null)
                return new ErrorResult("Güncellenecek dükkan bulunamadı.");
            var dtoChairIds = dto.Chairs.Select(c => c.Id).ToList();
            var conflict = await appointmentDal.AnyAsync(a =>
                dtoChairIds.Contains(a.ChairId) && (a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Pending));
            if (conflict)
            {
                return new ErrorResult("Bu dükkana ait randevu bulunmaktadır. Önce randevuların tamamlanması gerekir");
            }
            dto.Adapt(store);
            await barberStoreDal.Update(store);
            var existingChairs = await _barberStoreChairDal.GetAll(x => x.StoreId == dto.Id);
            foreach (var dtoManuelBarber in dto.ManualBarbers)
            {
                var match = await _manuelBarberDal.Get(x => x.Id == dtoManuelBarber.Id); 
                if (match != null)
                {
                    var changed =
                        !string.Equals(match.FirstName?.Trim(), dtoManuelBarber.FirstName?.Trim(), StringComparison.InvariantCultureIgnoreCase) ||
                        !string.Equals(match.ProfileImageUrl, dtoManuelBarber.ProfileImageUrl, StringComparison.Ordinal);

                    if (changed)
                    {
                        dtoManuelBarber.Adapt(match); 
                        await _manuelBarberDal.Update(match);
                    }
                }
                else
                {
                    var newMb = dtoManuelBarber.Adapt<ManuelBarber>();
                    newMb.StoreId = dto.Id;
                    await _manuelBarberDal.Add(newMb);
                }
            }

            foreach (var dtoChair in dto.Chairs)
            {
                var match = existingChairs.FirstOrDefault(x => x.Id == dtoChair.Id);
                if (match != null)
                {
                    if (HasChairChanged(match, dtoChair))
                    {
                        dtoChair.Adapt(match);
                        await _barberStoreChairDal.Update(match);
                    }
                }
                else
                {
                    var newChair = dtoChair.Adapt<BarberChair>();
                    newChair.StoreId = dto.Id;
                    await _barberStoreChairDal.Add(newChair);
                    if (HasChairChanged(newChair, dtoChair))
                    {
                        dtoChair.Adapt(newChair);
                        await _barberStoreChairDal.Update(newChair);
                    }

                }
            }
            var existingHours = await workingHourDal.GetAll(x => x.OwnerId == dto.Id);
            foreach (var dtoWh in dto.WorkingHours)
            {
                var match = existingHours.FirstOrDefault(x => x.DayOfWeek == dtoWh.DayOfWeek);
                if (match == null || WorkingHourChanged(match, dtoWh))
                {
                    match ??= new WorkingHour { OwnerId = dto.Id, DayOfWeek = dtoWh.DayOfWeek };
                    match.IsClosed = dtoWh.IsClosed;
                    match.StartTime = dtoWh.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(dtoWh.StartTime);
                    match.EndTime = dtoWh.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(dtoWh.EndTime);
                    await workingHourDal.Update(match);
                }
            }
            var existingOfferings = await _serviceOfferingDal.GetAll(x => x.OwnerId == dto.Id);
            var dtoNames = dto.Offerings
                .Select(o => o.ServiceName.Trim().ToLowerInvariant())
                .ToHashSet();
            var toDelete = existingOfferings
                .Where(db => !dtoNames.Contains(db.ServiceName.Trim().ToLowerInvariant()))
                .ToList();
            foreach (var off in toDelete)
            {
                await _serviceOfferingDal.Remove(off);
            }
            foreach (var dtoOffer in dto.Offerings)
            {
                var match = existingOfferings.FirstOrDefault(
                    x => x.ServiceName.Trim().ToLowerInvariant() == dtoOffer.ServiceName.Trim().ToLowerInvariant()
                );

                if (match != null)
                {
                    if (match.Price != dtoOffer.Price)
                    {
                        var updated = dtoOffer.Adapt<ServiceOffering>();
                        await _serviceOfferingDal.Update(updated);
                    }
                }
                else
                {
                    var newOffer = dtoOffer.Adapt<ServiceOffering>();
                    newOffer.OwnerId = dto.Id;
                    newOffer.CreatedAt = DateTime.Now;
                    await _serviceOfferingDal.Add(newOffer);
                }
            }
            scope.Complete();
            return new SuccessResult("Berber dükkanı başarıyla güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId)
        {
            var store = await barberStoreDal.Get(x => x.Id == storeId && x.BarberStoreUserId == currentUserId);
            if (store == null)
                return new ErrorResult("Dükkan bulunamadı veya silme yetkiniz yok.");

            await barberStoreDal.Remove(store);
            return new SuccessResult("Dükkan silindi.");
        }

        public async Task<IDataResult<BarberStoreDetailDto>> GetByIdAsync(Guid id)
        {
            var store = await barberStoreDal.GetByIdWithStatsAsync(id);
            if (store == null)
                return new ErrorDataResult<BarberStoreDetailDto>("Dükkan bulunamadı.");

            var dto = _mapper.Map<BarberStoreDetailDto>(store);
            return new SuccessDataResult<BarberStoreDetailDto>(dto);
        }

        public async Task<IDataResult<List<BarberStoreDetailDto>>> GetByCurrentUserAsync(Guid currentUserId)
        {
            var store = await barberStoreDal.GetByCurrentUserWithStatsAsync(currentUserId);
            if (store == null)
                return new ErrorDataResult<List<BarberStoreDetailDto>>("Dükkan bulunamadı.");
            var dto = _mapper.Map<List<BarberStoreDetailDto>>(store);
            return new SuccessDataResult<List<BarberStoreDetailDto>>(dto);
        }

        public async Task<IDataResult<List<BarberStoreListDto>>> GetNearbyStoresAsync(double lat, double lng, double distance)
        {
            var result = await barberStoreDal.GetNearbyStoresWithStatsAsync(lat, lng, distance);
            return new SuccessDataResult<List<BarberStoreListDto>>(result);
        }

        public async Task<IDataResult<BarberStoreOperationDetail>> GetByIdStoreOperation(Guid id)
        {
            var store = await barberStoreDal.GetByIdStoreOperation(id);
            if (store == null)
            {
                return new ErrorDataResult<BarberStoreOperationDetail>("Dükkan bulunamadı.");
            }
            return new SuccessDataResult<BarberStoreOperationDetail>(store);
        }

        private bool HasChairChanged(BarberChair entity, BarberChairUpdateDto dto)
        {

            if (entity.Name?.Trim().ToLowerInvariant() != dto.Name?.Trim().ToLowerInvariant())
            {
                if (dto.ManualBarberTempId == null)
                {
                    entity.Name = dto.Name;
                    entity.ManualBarberId = null;
                    entity.ManualBarber = null;
                }
                return true;
            }

            else if (entity.ManualBarberId != dto.ManualBarberTempId)
            {
                if (entity.ManualBarberId == null)
                {
                    entity.ManualBarberId = dto.ManualBarberTempId;

                }
                return true;


            }
            return false;
        }
        private bool WorkingHourChanged(WorkingHour entity, WorkingHourUpdateDto dto)
        {
            if (entity.IsClosed != dto.IsClosed)
                return true;

            if (dto.IsClosed)
                return false;

            if (!TimeSpan.TryParse(dto.StartTime, out var dtoStart) ||
                !TimeSpan.TryParse(dto.EndTime, out var dtoEnd))
            {
                return true;
            }

            return entity.StartTime != dtoStart || entity.EndTime != dtoEnd;
        }
    }
}
