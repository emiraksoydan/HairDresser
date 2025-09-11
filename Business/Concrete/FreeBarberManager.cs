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
    public class FreeBarberManager(IFreeBarberDal freeBarberDal, IWorkingHourDal workingHourDal, IServiceOfferingDal serviceOfferingDal, IAppointmentDal appointmentDal, IMapper _mapper) : IFreeBarberService
    {
        [ValidationAspect(typeof(FreeBarberCreateDtoValidator))]
        public async Task<IResult> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var newFreeBaarber = _mapper.Map<FreeBarber>(freeBarberCreateDto);
            newFreeBaarber.FreeBarberUserId = currentUserId;
            await freeBarberDal.Add(newFreeBaarber);
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
                return new ErrorResult(" serbest berber bulunamadı.");
            var findAppointment = await appointmentDal.Get(x => x.PerformerUserId == freeBarberUpdateDto.Id && (x.Status == Entities.Concrete.Enums.AppointmentStatus.Approved || x.Status == Entities.Concrete.Enums.AppointmentStatus.Pending));
            if(findAppointment == null)
                return new ErrorResult("  Randevu hazılığı bulunmaktadır");
            freeBarberUpdateDto.Adapt(store);
            await freeBarberDal.Update(store);
            var existingOfferings = await serviceOfferingDal.GetAll(x => x.OwnerId == freeBarberUpdateDto.Id);
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

    }
}
