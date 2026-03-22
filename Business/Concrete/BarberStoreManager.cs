
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Helpers;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Business;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;


namespace Business.Concrete
{
    public class BarberStoreManager(IBarberStoreDal barberStoreDal, IWorkingHourService workingHourService, IManuelBarberService _manuelBarberService, IBarberStoreChairService _barberStoreChairService, IServiceOfferingService _serviceOfferingService, IAppointmentService appointmentService, IFreeBarberDal freeBarberDal, BlockedHelper blockedHelper, IUserDal userDal) : IBarberStoreService
    {
        [SecuredOperation("BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(BarberStoreCreateDtoValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> Add(BarberStoreCreateDto dto, Guid currentUserId)
        {
            IResult result = BusinessRules.Run(BarberAttemptCore(dto.Chairs,c=>c.BarberId));
            if (result != null)
                return new ErrorDataResult<Guid>(result.Message);

            // Deneme süresindeyse maksimum 1 dükkan limiti
            var user = await userDal.Get(u => u.Id == currentUserId);
            bool isInTrial = user?.TrialEndDate > DateTime.UtcNow;
            bool hasSubscription = user?.SubscriptionEndDate.HasValue == true && user.SubscriptionEndDate.Value > DateTime.UtcNow;
            if (isInTrial && !hasSubscription)
            {
                var existingStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == currentUserId);
                if (existingStore != null)
                    return new ErrorDataResult<Guid>(Messages.TrialPanelLimitReached);
            }

            var store = await CreateStoreAsync(dto, currentUserId);
            await SaveManuelBarbersAsync(dto, store.Id);
            await SaveChairsAsync(dto, store.Id);
            await SaveOfferingsAsync(dto, store.Id);
            await SaveWorkingHoursAsync(dto, store.Id);
            return new SuccessDataResult<Guid>(store.Id, Messages.StoreCreatedSuccess);
        }

        [SecuredOperation("BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(BarberStoreUpdateDtoValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> Update(BarberStoreUpdateDto dto, Guid currentUserId)
        {
            var getBarber = await barberStoreDal.Get(x=>x.Id == dto.Id);

            if(getBarber.BarberStoreOwnerId != currentUserId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            IResult result = BusinessRules.Run(BarberAttemptCore(dto.Chairs, c => c.BarberId.ToString()));
            if (result != null)
                return result;

            var anyAppointCt = await appointmentService.AnyStoreControl(dto.Id);
            if (anyAppointCt.Data)
                return new ErrorResult(Messages.StoreHasActiveAppointments);
         

            dto.Adapt(getBarber);
            await barberStoreDal.Update(getBarber);
            await _serviceOfferingService.UpdateRange(dto.Offerings);
            await workingHourService.UpdateRangeAsync(dto.WorkingHours);

            return new SuccessResult(Messages.BarberStoreUpdatedSuccess);
        }

        [SecuredOperation("BarberStore")]
        [LogAspect]
        public async Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId)
        {

            return new SuccessResult("Dükkan silindi.");
        }

        public async Task<IDataResult<BarberStoreDetail>> GetByIdAsync(Guid id)
        {
            var result = await barberStoreDal.GetByIdStore(id);
            return new SuccessDataResult<BarberStoreDetail>(result);
        }

        [SecuredOperation("BarberStore")]
        public async Task<IDataResult<List<BarberStoreMineDto>>> GetByCurrentUserAsync(Guid currentUserId)
        {
            var result = await barberStoreDal.GetMineStores(currentUserId);
            return new SuccessDataResult<List<BarberStoreMineDto>>(result);
        }

        public async Task<IDataResult<List<BarberStoreGetDto>>> GetNearbyStoresAsync(double lat, double lon, double distance, Guid? currentUserId = null)
        {
            // Free barber kullanıcı tipinde ise ve panel oluşturmamışsa nearby stores döndürme
            if (currentUserId.HasValue)
            {
                // Kullanıcının free barber paneli var mı kontrol et
                var freeBarberPanel = await freeBarberDal.Get(x => x.FreeBarberUserId == currentUserId.Value);

                // Free barber paneli yoksa boş liste döndür
                if (freeBarberPanel == null)
                {
                    return new SuccessDataResult<List<BarberStoreGetDto>>(new List<BarberStoreGetDto>(), Messages.NearbyBarbersRetrieved);
                }
            }

            var result = await barberStoreDal.GetNearbyStoresAsync(lat, lon, distance, currentUserId);

            // Engellenmiş kullanıcıları filtrele
            if (currentUserId.HasValue && result != null && result.Count > 0)
            {
                result = await blockedHelper.FilterBlockedStoresAsync(
                    currentUserId,
                    result,
                    s => s.BarberStoreOwnerId ?? Guid.Empty
                );
            }

            return new SuccessDataResult<List<BarberStoreGetDto>>(result, Messages.NearbyBarbersRetrieved);
        }

        public async Task<IDataResult<List<BarberStoreGetDto>>> GetFilteredStoresAsync(FilterRequestDto filter)
        {
            var result = await barberStoreDal.GetFilteredStoresAsync(filter);

            // Engellenmiş kullanıcıları filtrele
            if (filter.CurrentUserId.HasValue && result != null && result.Count > 0)
            {
                result = await blockedHelper.FilterBlockedStoresAsync(
                    filter.CurrentUserId,
                    result,
                    s => s.BarberStoreOwnerId ?? Guid.Empty
                );
            }

            return new SuccessDataResult<List<BarberStoreGetDto>>(result, Messages.FilteredBarberStoresRetrieved);
        }

        public async Task<IDataResult<BarberStoreMineDto>> GetBarberStoreForUsers(Guid storeId)
        {
            var result = await barberStoreDal.GetBarberStoreForUsers(storeId);
            return new SuccessDataResult<BarberStoreMineDto>(result);
        }

        private IResult BarberAttemptCore<TChair>(List<TChair>? chairList,Func<TChair, string?> getBarberId)
        {
            if (chairList == null || chairList.Count == 0)
                return new SuccessResult();

            // BerberId'si dolu olan koltukları al
            var assigned = chairList
                .Select((c, i) => new { Index = i, BarberId = getBarberId(c) })
                .Where(x => !string.IsNullOrWhiteSpace(x.BarberId))
                .ToList();

            // Aynı berber birden fazla koltuğa atanmış mı?
            var duplicates = assigned
                .GroupBy(x => x.BarberId)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    BarberId = g.Key,
                    Chairs = g.Select(x => x.Index).ToList(),
                    Count = g.Count()
                })
                .ToList();

            if (duplicates.Count > 0)
            {
                return new ErrorResult(Messages.BarberAssignedToMultipleChairs);
            }
            return new SuccessResult();
        }


        private async Task<BarberStore> CreateStoreAsync(BarberStoreCreateDto dto, Guid currentUserId)
        {
            BarberStore store = dto.Adapt<BarberStore>();
            store.BarberStoreOwnerId = currentUserId;
            store.StoreNo = await GenerateUniqueStoreNoAsync();
            await barberStoreDal.Add(store);
            return store;
        }

        private async Task<string> GenerateUniqueStoreNoAsync()
        {
            var random = new Random();
            string storeNo;
            do
            {
                // 6 haneli rastgele sayı (100000-999999)
                storeNo = random.Next(100000, 999999).ToString();
                var existing = await barberStoreDal.Get(s => s.StoreNo == storeNo);
                if (existing == null) break;
            } while (true);
            return storeNo;
        }

        private async Task SaveManuelBarbersAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var manuelBarberDtos = dto.ManuelBarbers;
            if (manuelBarberDtos?.Count == 0)
                return;
            await _manuelBarberService.AddRangeAsync(manuelBarberDtos!, storeId);
        }

        private async Task SaveWorkingHoursAsync(BarberStoreCreateDto dto, Guid storeId)
        {

            var workingHours = (dto.WorkingHours ?? new List<WorkingHourCreateDto>()).Adapt<List<WorkingHour>>();
            if (workingHours.Count > 0)
            {
                foreach (var workingHour in workingHours)
                    workingHour.OwnerId = storeId;
                await workingHourService.AddRangeAsync(workingHours);
            }
        }

        private async Task SaveOfferingsAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var offers = (dto.Offerings ?? new List<ServiceOfferingCreateDto>()).Adapt<List<ServiceOffering>>();

            if (offers != null && offers.Count > 0)
            {
                foreach (var o in offers)
                    o.OwnerId = storeId;
                await _serviceOfferingService.AddRangeAsync(offers);
            }
        }

        private async Task SaveChairsAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var chairs = (dto.Chairs ?? new List<BarberChairCreateDto>()).Adapt<List<BarberChair>>();
            if (chairs != null && chairs.Count > 0)
            {
                foreach (var c in chairs)
                    c.StoreId = storeId;
                await _barberStoreChairService.AddRangeAsync(chairs);
            }
        }

   
    }
}
