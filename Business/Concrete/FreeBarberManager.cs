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
using Entities.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class FreeBarberManager(IFreeBarberDal freeBarberDal, IWorkingHourDal workingHourDal, IServiceOfferingDal serviceOfferingDal, IMapper _mapper) : IFreeBarberService
    {
        [ValidationAspect(typeof(FreeBarberCreateDtoValidator))]
        public async Task<IResult> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var newFreeBaarber = _mapper.Map<FreeBarber>(freeBarberCreateDto);
            newFreeBaarber.FreeBarberUserId = currentUserId;
            await freeBarberDal.Add(newFreeBaarber);
            var whList = freeBarberCreateDto.WorkingHours.Select(r => new WorkingHour
            {
                OwnerId = newFreeBaarber.Id,
                DayOfWeek = r.DayOfWeek,
                StartTime = r.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(r.StartTime),
                EndTime = r.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(r.EndTime),
                IsClosed = r.IsClosed
            }).ToList();
            await workingHourDal.AddRange(whList);
            var offerEntities = freeBarberCreateDto.Offerings?.Select(o =>
            {
                var e = o.Adapt<ServiceOffering>();
                e.OwnerId = newFreeBaarber.Id;
                return e;
            }).ToList() ?? new();
            if (offerEntities.Any())
                await serviceOfferingDal.AddRange(offerEntities);
            scope.Complete();
            return new SuccessResult("Serbest berber portalı başarıyla oluşturuldu.");
        }
        [ValidationAspect(typeof(FreeBarberCreateDtoValidator))]
        public async Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var store = await freeBarberDal.Get(x => x.Id == freeBarberUpdateDto.Id);
            if (store == null)
                return new ErrorResult("Güncellenecek serbest berber bulunamadı.");
            freeBarberUpdateDto.Adapt(store);
            await freeBarberDal.Update(store);
            var existingHours = await workingHourDal.GetAll(x => x.OwnerId == freeBarberUpdateDto.Id);
            foreach (var dtoWh in freeBarberUpdateDto.WorkingHours)
            {
                var match = existingHours.FirstOrDefault(x => x.DayOfWeek == dtoWh.DayOfWeek);
                if (match == null || WorkingHourChanged(match, dtoWh))
                {
                    match ??= new WorkingHour { OwnerId = freeBarberUpdateDto.Id, DayOfWeek = dtoWh.DayOfWeek };
                    match.IsClosed = dtoWh.IsClosed;
                    match.StartTime = dtoWh.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(dtoWh.StartTime);
                    match.EndTime = dtoWh.IsClosed ? TimeSpan.Zero : TimeSpan.Parse(dtoWh.EndTime);
                    await workingHourDal.Update(match);
                }
            }
            var existingOfferings = await serviceOfferingDal.GetAll(x => x.OwnerId == freeBarberUpdateDto.Id);

            // DTO'dan gelen servisleri Id ile eşleyebiliyorsan Id ile eşle:
            var existingById = existingOfferings.ToDictionary(x => x.Id);

            var incomingIds = freeBarberUpdateDto.Offerings
               .Where(o => o.Id != Guid.Empty) 
               .Select(o => o.Id)
               .ToHashSet();

            var toDelete = existingOfferings
                .Where(db => !incomingIds.Contains(db.Id))
                .ToList();
            foreach (var off in toDelete)
                await serviceOfferingDal.Remove(off);

            foreach (var dtoOffer in freeBarberUpdateDto.Offerings)
            {
                if (dtoOffer.Id != null && existingById.TryGetValue(dtoOffer.Id.Value, out var entity))
                {
    
                    entity.ServiceName = dtoOffer.ServiceName;
                    entity.Price = dtoOffer.Price;
                    entity.UpdatedAt = DateTime.Now;

                    await serviceOfferingDal.Update(entity);
                }
                else
                {
                    var newOffer = dtoOffer.Adapt<ServiceOffering>();
                    newOffer.OwnerId = freeBarberUpdateDto.Id;
                    newOffer.CreatedAt = DateTime.Now;
                    await serviceOfferingDal.Add(newOffer);
                }
            }
            scope.Complete();
            return new SuccessResult("Serbest berber güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid storeId)
        {
            var store = await freeBarberDal.Get(x => x.Id == storeId);
            if (store == null)
                return new ErrorResult("Serbest berber bulunamadı veya silme yetkiniz yok.");

            await freeBarberDal.Remove(store);
            return new SuccessResult("Serbest berber silindi.");
        }

        public async Task<IDataResult<FreeBarberDetailDto>> GetByIdAsync(Guid id)
        {
            var store = await freeBarberDal.GetByIdWithStatsAsync(id);
            if (store == null)
                return new ErrorDataResult<FreeBarberDetailDto>("Serbest berber bulunamadı.");

            var dto = _mapper.Map<FreeBarberDetailDto>(store);
            return new SuccessDataResult<FreeBarberDetailDto>(dto);
        }

        public async Task<IDataResult<FreeBarberDetailDto>> GetMyPanel(Guid currentUserId)
        {
            var store = await freeBarberDal.GetByFreeBarberPanel(currentUserId);
            if (store == null)
                return new ErrorDataResult<FreeBarberDetailDto>("Serbest berber  bulunamadı.");

            var dto = _mapper.Map<FreeBarberDetailDto>(store);
            return new SuccessDataResult<FreeBarberDetailDto>(dto);
        }

        public async Task<IDataResult<List<FreeBarberListDto>>> GetNearbyStoresAsync(double lat, double lng, double distance)
        {
            var result = await freeBarberDal.GetNearbyFreeBarberWithStatsAsync(lat, lng, distance);
            return new SuccessDataResult<List<FreeBarberListDto>>(result);
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
