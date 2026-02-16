using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Helpers;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;


namespace Business.Concrete
{
    public class FreeBarberManager(IFreeBarberDal freeBarberDal, IAppointmentService _appointmentService, IServiceOfferingService _serviceOfferingService, BlockedHelper blockedHelper) : IFreeBarberService
    {
        [SecuredOperation("FreeBarber")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        [ValidationAspect(typeof(FreeBarberDtoValidator))]
        public async Task<IDataResult<Guid>> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId)
        {
            // Kullanıcının zaten bir FreeBarber paneli var mı kontrol et
            var existingPanel = await freeBarberDal.Get(x => x.FreeBarberUserId == currentUserId);
            if (existingPanel != null)
                return new ErrorDataResult<Guid>(Messages.FreeBarberPanelAlreadyExists);

            var entity = freeBarberCreateDto.Adapt<FreeBarber>();
            entity.FreeBarberUserId = currentUserId;
            await freeBarberDal.Add(entity);
            await SaveOfferingsAsync(freeBarberCreateDto, entity.Id);
            return new SuccessDataResult<Guid>(entity.Id, Messages.FreeBarberPortalCreatedSuccess);
        }
        [SecuredOperation("FreeBarber")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        [ValidationAspect(typeof(FreeBarberDtoValidator))]
        public async Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto,Guid currentUserId)
        {
            var existingEntity = await freeBarberDal.Get(x=>x.Id == freeBarberUpdateDto.Id);
            if (existingEntity.FreeBarberUserId != currentUserId)
                return new ErrorResult(Messages.FreeBarberUpdateUnauthorized);
            var appointCont = await _appointmentService.AnyControl(freeBarberUpdateDto.Id);
            if(appointCont.Data)
                return new ErrorResult(Messages.FreeBarberHasActiveAppointmentUpdate);

            freeBarberUpdateDto.Adapt(existingEntity);
            await freeBarberDal.Update(existingEntity);
            await _serviceOfferingService.UpdateRange(freeBarberUpdateDto.Offerings);
            return new SuccessResult("Serbest berber güncellendi.");
        }

        [SecuredOperation("FreeBarber")]
        [LogAspect]
        public async Task<IResult> DeleteAsync(Guid storeId)
        {

            return new SuccessResult(Messages.FreeBarberDeletedSuccess);
        }

        [SecuredOperation("FreeBarber")]
        [LogAspect]
        [ValidationAspect(typeof(UpdateLocationDtoValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UpdateLocationAsync(UpdateLocationDto dto, Guid currentUserId)
        {
            // FreeBarber'ı CurrentUserId ile bul (Id yerine)
            var getBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == currentUserId);
            if (getBarber == null)
                return new ErrorResult(Messages.BarberNotFound);

            getBarber.Latitude = dto.Latitude;
            getBarber.Longitude = dto.Longitude;

            await freeBarberDal.Update(getBarber);

            return new SuccessResult(Messages.LocationUpdatedSuccess);

        }
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        public async Task<IDataResult<FreeBarberMinePanelDto>> GetMyPanel(Guid currentUserId)
        {
            var result = await freeBarberDal.GetMyPanel(currentUserId);
            if (result == null) {
                return new ErrorDataResult<FreeBarberMinePanelDto>(Messages.PanelGetFailed);
            }
            return new SuccessDataResult<FreeBarberMinePanelDto>(result);
        }

        public async Task<IDataResult<List<FreeBarberGetDto>>> GetNearbyFreeBarberAsync(double lat, double lon, double distance, Guid? currentUserId = null)
        {
            var getFreeBarberResult = await freeBarberDal.GetNearbyFreeBarberAsync(lat, lon, distance, currentUserId);

            // Engellenmiş kullanıcıları filtrele
            if (currentUserId.HasValue && getFreeBarberResult != null && getFreeBarberResult.Count > 0)
            {
                getFreeBarberResult = await blockedHelper.FilterBlockedUsersAsync(
                    currentUserId,
                    getFreeBarberResult,
                    fb => fb.FreeBarberUserId
                );
            }

            return new SuccessDataResult<List<FreeBarberGetDto>>(getFreeBarberResult);
        }

        public async Task<IDataResult<List<FreeBarberGetDto>>> GetFilteredFreeBarbersAsync(FilterRequestDto filter)
        {
            var result = await freeBarberDal.GetFilteredFreeBarbersAsync(filter);

            // Engellenmiş kullanıcıları filtrele
            if (filter.CurrentUserId.HasValue && result != null && result.Count > 0)
            {
                result = await blockedHelper.FilterBlockedUsersAsync(
                    filter.CurrentUserId,
                    result,
                    fb => fb.FreeBarberUserId
                );
            }

            return new SuccessDataResult<List<FreeBarberGetDto>>(result, Messages.FilteredFreeBarbersRetrieved);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        public async Task<IDataResult<FreeBarberMinePanelDetailDto>> GetMyPanelDetail(Guid panelId)
        {
            var result = await freeBarberDal.GetPanelDetailById(panelId);
            if (result == null) {
                return new ErrorDataResult<FreeBarberMinePanelDetailDto>(Messages.PanelDetailGetFailed);
            }
            return new SuccessDataResult<FreeBarberMinePanelDetailDto>(result);
        }

        public async Task<IDataResult<FreeBarberMinePanelDto>> GetFreeBarberForUsers(Guid freeBarberId)
        {
            var result = await freeBarberDal.GetFreeBarberForUsers(freeBarberId);
            return new SuccessDataResult<FreeBarberMinePanelDto>(result);
        }

        private async Task SaveOfferingsAsync(FreeBarberCreateDto dto, Guid panelId)
        {
            var offers = (dto.Offerings ?? new List<ServiceOfferingCreateDto>()).Adapt<List<ServiceOffering>>();

            if (offers != null && offers.Count > 0)
            {
                foreach (var o in offers)
                    o.OwnerId = panelId;
                await _serviceOfferingService.AddRangeAsync(offers);
            }
        }

      

        [SecuredOperation("FreeBarber")]
        [LogAspect]
        public async Task<IResult> UpdateAvailabilityAsync(bool isAvailable, Guid currentUserId)
        {
            var existingPanel = await freeBarberDal.Get(x => x.FreeBarberUserId == currentUserId);
            if (existingPanel == null)
                return new ErrorResult(Messages.BarberNotFound);
            

            var hasActiveAppointments = await _appointmentService.AnyControl(currentUserId);
            if (hasActiveAppointments.Data)    
                return new ErrorResult(Messages.FreeBarberHasActiveAppointmentUpdate); 
            

            existingPanel.IsAvailable = isAvailable;
            await freeBarberDal.Update(existingPanel);
            return new SuccessResult("Müsaitlik durumu güncellendi.");
        }
    }
}
