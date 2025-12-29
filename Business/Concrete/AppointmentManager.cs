using Business.Abstract;
using Business.Helpers;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Business;
using Core.Utilities.Configuration;
using Core.Utilities.Helpers;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Business.Concrete
{
    public class AppointmentManager(
        IAppointmentDal appointmentDal,
        IBarberStoreDal barberStoreDal,
        IFreeBarberDal freeBarberDal,
        IBarberStoreChairDal chairDal,
        IServiceOfferingDal offeringDal,
        IAppointmentServiceOffering apptOfferingDal,
        IChatThreadDal threadDal,
        IWorkingHourDal workingHourDal,
        IAppointmentNotifyService notifySvc,
        INotificationService notificationService,
        IRealTimePublisher realtime,
        IChatService chatService,
        IOptions<AppointmentSettings> appointmentSettings,
        IBadgeUpdateService badgeUpdateService,
        IUserDal userDal,
        AppointmentBusinessRules businessRules
    ) : IAppointmentService
    {
        private static readonly AppointmentStatus[] Active = [AppointmentStatus.Pending, AppointmentStatus.Approved];
        private readonly AppointmentSettings _settings = appointmentSettings.Value;
        
        // Timeout S├╝releri:
        // - ─░ste─şime G├Âre: _settings.PendingTimeoutMinutes (5 dakika)
        // - D├╝kkan Se├ğ (toplam): StoreSelectionTotalMinutes (30 dakika)
        // - D├╝kkan onay─▒: StoreSelectionStepMinutes (5 dakika, toplam 30 dk'ya dahil)
        // - M├╝┼şteri onay─▒: Yeni s├╝re yok, toplam 30 dakikaya dahil
        private const int StoreSelectionTotalMinutes = 30;
        private const int StoreSelectionStepMinutes = 5;

        // ---------------- QUICK CHECKS ----------------

        public async Task<IDataResult<bool>> AnyControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x =>
                (x.FreeBarberUserId == id || x.CustomerUserId == id) &&
                Active.Contains(x.Status));

            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyChairControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x =>
                x.ChairId == id && Active.Contains(x.Status));

            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyStoreControl(Guid id)
        {
            var store = await barberStoreDal.Get(x => x.Id == id);
            if (store is null) return new ErrorDataResult<bool>(false, Messages.StoreNotFound);

            // Not: Store'un birden fazla active randevusu OLAB─░L─░R demi┼ştin.
            // Bu methodu sadece "bilgi" ama├ğl─▒ tutuyorum.
            var has = await appointmentDal.AnyAsync(x =>
                x.BarberStoreUserId == store.BarberStoreOwnerId &&
                Active.Contains(x.Status));

            return new SuccessDataResult<bool>(has);
        }


        public async Task<IDataResult<List<ChairSlotDto>>> GetAvailibity(Guid storeId, DateOnly dateOnly, CancellationToken ct = default)
        {
            var res = await appointmentDal.GetAvailibilitySlot(storeId, dateOnly, ct);
            return new SuccessDataResult<List<ChairSlotDto>>(res);
        }

        public async Task<IDataResult<bool>> AnyManuelBarberControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x =>
                x.ManuelBarberId == id && Active.Contains(x.Status));

            return new SuccessDataResult<bool>(hasBlocking);
        }


        public async Task<IDataResult<List<AppointmentGetDto>>> GetAllAppointmentByFilter(Guid currentUserId, AppointmentFilter appointmentFilter)
        {
            var result = await appointmentDal.GetAllAppointmentByFilter(currentUserId, appointmentFilter);
            return new SuccessDataResult<List<AppointmentGetDto>>(result);
        }

        // ---------------- CREATE: CUSTOMER -> FREEBARBER (NEW) ----------------
        
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateCustomerToFreeBarberAsync(Guid customerUserId, CreateAppointmentRequestDto req)
        {
            // Validasyonlar
            if (!req.FreeBarberUserId.HasValue)
                return new ErrorDataResult<Guid>(Messages.FreeBarberUserIdRequired);
            
            if (!req.StoreSelectionType.HasValue)
                return new ErrorDataResult<Guid>("StoreSelectionType se├ğilmelidir.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.StoreSelection && string.IsNullOrWhiteSpace(req.Note))
                return new ErrorDataResult<Guid>("Randevu notu zorunludur.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.StoreSelection && (req.StoreId != Guid.Empty && req.StoreId != default))
                return new ErrorDataResult<Guid>("D├╝kkan Se├ğ senaryosunda storeId g├Ânderilemez.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.StoreSelection && req.ServiceOfferingIds != null && req.ServiceOfferingIds.Count > 0)
                return new ErrorDataResult<Guid>("D├╝kkan Se├ğ senaryosunda hizmet se├ğilemez.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.CustomRequest && (req.ServiceOfferingIds == null || req.ServiceOfferingIds.Count == 0))
                return new ErrorDataResult<Guid>(Messages.ServiceOfferingRequired);
            
            if (req.StoreSelectionType.Value == StoreSelectionType.CustomRequest && (req.StoreId != Guid.Empty && req.StoreId != default))
                return new ErrorDataResult<Guid>("─░ste─şime G├Âre se├ğene─şinde d├╝kkan se├ğilemez.");
            
            if (!req.RequestLatitude.HasValue || !req.RequestLongitude.HasValue)
                return new ErrorDataResult<Guid>(Messages.LocationRequired);
            
            // FreeBarber entity'sini al
            var fbEntity = await freeBarberDal.Get(x => x.FreeBarberUserId == req.FreeBarberUserId.Value);
            if (fbEntity is null) return new ErrorDataResult<Guid>(Messages.FreeBarberNotFound);
            
            // Business Rules kontrol├╝
            // StoreSelection senaryosunda FreeBarber me┼şgul olsa bile d├╝kkana randevu iste─şi g├Ânderebilir
            var businessRulesList = new List<Func<Task<IResult>>>
            {
                async () => await businessRules.CheckUserIsCustomer(customerUserId),
                async () => await businessRules.CheckFreeBarberExists(req.FreeBarberUserId.Value),
                async () => businessRules.CheckDistance(req.RequestLatitude.Value, req.RequestLongitude.Value, fbEntity.Latitude, fbEntity.Longitude, Messages.FreeBarberDistanceExceeded),
                async () => await businessRules.CheckActiveAppointmentRules(customerUserId, req.FreeBarberUserId, null, AppointmentRequester.Customer)
            };
            
            // StoreSelection senaryosunda me┼şgul kontrol├╝ yapma
            if (req.StoreSelectionType.Value != StoreSelectionType.StoreSelection)
            {
                businessRulesList.Insert(2, async () => await businessRules.CheckFreeBarberAvailable(req.FreeBarberUserId.Value));
            }
            
            IResult? result = await BusinessRules.RunAsync(businessRulesList.ToArray());
            
            if (result != null)
                return new ErrorDataResult<Guid>(result.Message);
            
            // Service offering kontrol├╝
            if (req.StoreSelectionType.Value == StoreSelectionType.CustomRequest)
            {
                var offeringRes = await EnsureServiceOfferingsBelongToOwnerAsync(req.ServiceOfferingIds, fbEntity.Id);
                if (!offeringRes.Success) return new ErrorDataResult<Guid>(offeringRes.Message);
            }
            
            // StoreSelectionType'a g├Âre timeout belirle
            int timeoutMinutes = req.StoreSelectionType.Value == StoreSelectionType.CustomRequest 
                ? _settings.PendingTimeoutMinutes
                : StoreSelectionTotalMinutes;
            
            // Randevu olu┼ştur
            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = null,
                AppointmentDate = req.AppointmentDate,
                StartTime = req.StartTime,
                EndTime = req.EndTime,
                CustomerUserId = customerUserId,
                FreeBarberUserId = req.FreeBarberUserId.Value,
                BarberStoreUserId = null,
                RequestedBy = AppointmentRequester.Customer,
                Status = AppointmentStatus.Pending,
                StoreDecision = null,
                FreeBarberDecision = DecisionStatus.Pending,
                CustomerDecision = null,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(timeoutMinutes),
                Note = req.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            appt.StoreSelectionType = req.StoreSelectionType.Value;
            if (req.StoreSelectionType.Value == StoreSelectionType.StoreSelection)
            {
                // D├╝kkan Se├ğ: FreeBarber 30dk i├ğinde red edebilir, d├╝kkan hen├╝z yok
                appt.AppointmentDate = null;
                appt.StartTime = null;
                appt.EndTime = null;
            }
            // ─░ste─şime G├Âre senaryosunda da decision'lar null kal─▒r
            // FreeBarber karar verdi─şinde Customer'a bildirim gider
            
            try
            {
                await appointmentDal.Add(appt);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2627)
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }
            
            var lockRes = await SetFreeBarberAvailabilityAsync(fbEntity, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);
            
            await FinalizeAppointmentCreationAsync(appt, req.ServiceOfferingIds, customerUserId);
            
            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- CREATE: CUSTOMER -> STORE ----------------
        
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateCustomerToStoreAndFreeBarberControlAsync(Guid customerUserId, CreateAppointmentRequestDto req)
        {
            // Validasyonlar
            if (req.FreeBarberUserId.HasValue)
                return new ErrorDataResult<Guid>(Messages.FreeBarberNotAllowedForStoreAppointment);
            
            if (!req.ChairId.HasValue)
                return new ErrorDataResult<Guid>(Messages.ChairRequired);
            
            if (req.StartTime is null || req.EndTime is null)
                return new ErrorDataResult<Guid>(Messages.StartTimeEndTimeRequired);
            
            if (!req.AppointmentDate.HasValue)
                return new ErrorDataResult<Guid>(Messages.InvalidDate);
            
            if (!req.RequestLatitude.HasValue || !req.RequestLongitude.HasValue)
                return new ErrorDataResult<Guid>(Messages.LocationRequired);
            
            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            var appointmentDate = req.AppointmentDate.Value;
            
            // Store ve Chair entity'lerini al
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFound);
            
            var chair = await chairDal.Get(c => c.Id == req.ChairId.Value && c.StoreId == req.StoreId);
            if (chair is null) return new ErrorDataResult<Guid>(Messages.ChairNotInStore);
            
            // Business Rules kontrol├╝ - Core.Utilities.Business.BusinessRules.RunAsync kullan─▒m─▒
            IResult? result = await BusinessRules.RunAsync(
                async () => await businessRules.CheckUserIsCustomer(customerUserId),
                async () => await businessRules.CheckStoreExists(req.StoreId),
                async () => await businessRules.CheckChairBelongsToStore(req.ChairId.Value, req.StoreId),
                async () => businessRules.CheckTimeRangeValid(start, end),
                async () => businessRules.CheckDateNotPast(appointmentDate, start),
                async () => businessRules.CheckDistance(req.RequestLatitude.Value, req.RequestLongitude.Value, store.Latitude, store.Longitude, Messages.CustomerDistanceExceeded),
                async () => await businessRules.CheckActiveAppointmentRules(customerUserId, null, store.BarberStoreOwnerId, AppointmentRequester.Customer),
                async () => await EnsureStoreIsOpenAsync(req.StoreId, appointmentDate, start, end),
                async () => await EnsureChairNoOverlapAsync(req.ChairId.Value, appointmentDate, start, end)
            );
            
            if (result != null)
                return new ErrorDataResult<Guid>(result.Message);
            
            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = req.ChairId.Value,
                AppointmentDate = appointmentDate,
                StartTime = start,
                EndTime = end,
                BarberStoreUserId = store.BarberStoreOwnerId,
                CustomerUserId = customerUserId,
                FreeBarberUserId = null,
                ManuelBarberId = chair.ManuelBarberId,
                RequestedBy = AppointmentRequester.Customer,
                Status = AppointmentStatus.Pending,
                StoreDecision = DecisionStatus.Pending,
                FreeBarberDecision = null,
                CustomerDecision = null,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(_settings.PendingTimeoutMinutes),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            try
            {
                await appointmentDal.Add(appt);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2627)
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }
            
            await CreateAppointmentServiceOfferingsAsync(appt.Id, req.ServiceOfferingIds);
            await EnsureThreadAndPushCreatedAsync(appt);
            await notifySvc.NotifyWithAppointmentAsync(appt, NotificationType.AppointmentCreated, actorUserId: customerUserId);
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();
            
            return new SuccessDataResult<Guid>(appt.Id);
        }
        // ---------------- CREATE: FREEBARBER -> STORE ----------------
        
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req)
        {
            // Validasyonlar
            if (req.StartTime is null || req.EndTime is null)
                return new ErrorDataResult<Guid>(Messages.StartTimeEndTimeRequired);
            
            if (!req.AppointmentDate.HasValue)
                return new ErrorDataResult<Guid>(Messages.InvalidDate);
            
            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            var appointmentDate = req.AppointmentDate.Value;
            
            // Store ve FreeBarber entity'lerini al
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFoundEnglish);
            
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            if (fb is null) return new ErrorDataResult<Guid>(Messages.FreeBarberNotFound);
            
            // Business Rules kontrol├╝ - Core.Utilities.Business.BusinessRules.RunAsync kullan─▒m─▒
            IResult? result = await BusinessRules.RunAsync(
                async () => await businessRules.CheckStoreExists(req.StoreId),
                async () => await businessRules.CheckFreeBarberExists(freeBarberUserId),
                async () => await businessRules.CheckFreeBarberAvailable(freeBarberUserId),
                async () => businessRules.CheckTimeRangeValid(start, end),
                async () => businessRules.CheckDateNotPast(appointmentDate, start),
                async () => businessRules.CheckDistance(fb.Latitude, fb.Longitude, store.Latitude, store.Longitude, Messages.FreeBarberStoreDistanceExceeded),
                async () => await businessRules.CheckActiveAppointmentRules(null, freeBarberUserId, store.BarberStoreOwnerId, AppointmentRequester.FreeBarber),
                async () => await EnsureStoreIsOpenAsync(req.StoreId, appointmentDate, start, end)
            );
            
            if (result != null)
                return new ErrorDataResult<Guid>(result.Message);

            // chair se├ğilmi┼şse storeÔÇÖa ait + overlap kontrol
            if (req.ChairId.HasValue)
            {
                var chairResult = await BusinessRules.RunAsync(
                    async () => await businessRules.CheckChairBelongsToStore(req.ChairId.Value, req.StoreId),
                    async () => await EnsureChairNoOverlapAsync(req.ChairId.Value, appointmentDate, start, end)
                );
                
                if (chairResult != null)
                    return new ErrorDataResult<Guid>(chairResult.Message);
            }

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = req.ChairId,
                BarberStoreUserId = store.BarberStoreOwnerId,
                CustomerUserId = null,
                FreeBarberUserId = freeBarberUserId,
                ManuelBarberId = null,
                AppointmentDate = appointmentDate,
                StartTime = start,
                EndTime = end,
                RequestedBy = AppointmentRequester.FreeBarber,
                Status = AppointmentStatus.Pending,
                FreeBarberDecision = null,
                StoreDecision = DecisionStatus.Pending,
                CustomerDecision = null,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(_settings.PendingTimeoutMinutes),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            try
            {
                await appointmentDal.Add(appt);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2627)
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }
            
            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);
            
            await FinalizeAppointmentCreationAsync(appt, req.ServiceOfferingIds, freeBarberUserId);

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- CREATE: STORE -> FREEBARBER (CALL) ----------------

        // ---------------- CREATE: STORE -> FREEBARBER (SIMPLE CALL) ----------------
        [ValidationAspect(typeof(CreateStoreToFreeBarberRequestDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateStoreToFreeBarberRequestDto req)
        {
            // Business Rules kontrol├╝
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId && x.BarberStoreOwnerId == storeOwnerUserId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFoundOrNotOwner);
            
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == req.FreeBarberUserId);
            if (fb is null) return new ErrorDataResult<Guid>(Messages.FreeBarberNotFound);

            // Free barber'─▒n pending/approved randevusu varsa berber ├ğa─şr─▒ld─▒ olsun (me┼şgul)
            // CheckFreeBarberAvailable kontrol├╝n├╝ atl─▒yoruz - pending/approved randevusu varsa yeni randevu olu┼şturulabilir
            var hasActiveAppointment = await appointmentDal.AnyAsync(x =>
                x.FreeBarberUserId == req.FreeBarberUserId &&
                Active.Contains(x.Status));
            
            // E─şer aktif randevu varsa free barber me┼şgul olmal─▒
            if (hasActiveAppointment && fb.IsAvailable)
            {
                var freeBarberLockRes = await SetFreeBarberAvailabilityAsync(fb, false);
                if (!freeBarberLockRes.Success) return new ErrorDataResult<Guid>(freeBarberLockRes.Message);
            }
            // E─şer aktif randevu yoksa ve free barber me┼şgul de─şilse, CheckFreeBarberAvailable kontrol├╝ yap
            else if (!hasActiveAppointment)
            {
                var availableResult = await businessRules.CheckFreeBarberAvailable(req.FreeBarberUserId);
                if (!availableResult.Success) return new ErrorDataResult<Guid>(availableResult.Message);
            }

            IResult? result = await BusinessRules.RunAsync(
                async () => await businessRules.CheckStoreOwnership(req.StoreId, storeOwnerUserId),
                async () => await businessRules.CheckFreeBarberExists(req.FreeBarberUserId),
                async () => businessRules.CheckDistance(store.Latitude, store.Longitude, fb.Latitude, fb.Longitude, Messages.StoreFreeBarberDistanceExceeded),
                async () => await businessRules.CheckActiveAppointmentRules(null, req.FreeBarberUserId, storeOwnerUserId, AppointmentRequester.Store),
                async () => await EnsureStoreIsOpenNowAsync(req.StoreId)
            );

            if (result != null)
                return new ErrorDataResult<Guid>(result.Message);

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = null,
                BarberStoreUserId = storeOwnerUserId,
                CustomerUserId = null,
                FreeBarberUserId = req.FreeBarberUserId,
                ManuelBarberId = null,
                AppointmentDate = null,
                StartTime = null,
                EndTime = null,
                RequestedBy = AppointmentRequester.Store,
                Status = AppointmentStatus.Pending,
                StoreDecision = null,
                FreeBarberDecision = DecisionStatus.Pending,
                CustomerDecision = null,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(_settings.PendingTimeoutMinutes),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await appointmentDal.Add(appt);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2627)
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await FinalizeAppointmentCreationAsync(appt, serviceOfferingIds: null, storeOwnerUserId);

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- ADD STORE TO EXISTING CUSTOMER->FREEBARBER APPOINTMENT ----------------

        /// <summary>
        /// Free barber, m├╝┼şteri randevusuna d├╝kkan ekler (D├╝kkan Se├ğ senaryosu)
        /// </summary>
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> AddStoreToExistingAppointmentAsync(Guid freeBarberUserId, Guid appointmentId, Guid storeId, Guid chairId, DateOnly appointmentDate, TimeSpan startTime, TimeSpan endTime, List<Guid> serviceOfferingIds)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);

            // Sadece free barber bu i┼şlemi yapabilir
            if (appt.FreeBarberUserId != freeBarberUserId)
                return new ErrorDataResult<bool>(false, Messages.Unauthorized);

            // Sadece Customer -> FreeBarber randevusu olmal─▒ (StoreSelectionType.StoreSelection)
            if (appt.StoreSelectionType != StoreSelectionType.StoreSelection)
                return new ErrorDataResult<bool>(false, "Bu randevuya d├╝kkan eklenemez.");

            if (serviceOfferingIds == null || serviceOfferingIds.Count == 0)
                return new ErrorDataResult<bool>(false, Messages.ServiceOfferingRequired);

            if (appt.CustomerUserId == null || appt.BarberStoreUserId != null)
                return new ErrorDataResult<bool>(false, "Bu randevuya d├╝kkan eklenemez.");

            // Randevu hala pending olmal─▒
            if (appt.Status != AppointmentStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentNotPendingStatus);

            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;

            // Business Rules kontrol├╝
            var store = await barberStoreDal.Get(x => x.Id == storeId);
            if (store is null) return new ErrorDataResult<bool>(false, Messages.StoreNotFoundEnglish);

            var chair = await chairDal.Get(c => c.Id == chairId && c.StoreId == storeId);
            if (chair is null) return new ErrorDataResult<bool>(false, Messages.ChairNotInStore);

            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            if (fb is null) return new ErrorDataResult<bool>(false, Messages.FreeBarberNotFound);

            IResult? result = await BusinessRules.RunAsync(
                async () => businessRules.CheckTimeRangeValid(startTime, endTime),
                async () => businessRules.CheckDateNotPast(appointmentDate, startTime),
                async () => await businessRules.CheckStoreExists(storeId),
                async () => await businessRules.CheckChairBelongsToStore(chairId, storeId),
                async () => await businessRules.CheckFreeBarberExists(freeBarberUserId),
                async () => businessRules.CheckDistance(fb.Latitude, fb.Longitude, store.Latitude, store.Longitude, Messages.FreeBarberStoreDistanceExceeded),
                async () => await EnsureStoreIsOpenAsync(storeId, appointmentDate, startTime, endTime),
                async () => await EnsureChairNoOverlapAsync(chairId, appointmentDate, startTime, endTime)
            );

            if (result != null)
                return new ErrorDataResult<bool>(false, result.Message);

            var offeringRes = await EnsureServiceOfferingsBelongToOwnerAsync(serviceOfferingIds, store.Id);
            if (!offeringRes.Success) return new ErrorDataResult<bool>(false, offeringRes.Message);

            // Randevuya d├╝kkan bilgisini ekle
            appt.BarberStoreUserId = store.BarberStoreOwnerId;
            appt.ChairId = chairId;
            // D├╝kkan i├ğin 5 dakikal─▒k onay s├╝resi (ama toplam 30 dakikaya dahil)
            SetStoreSelectionStepExpiry(appt);
            appt.AppointmentDate = appointmentDate;
            appt.StartTime = startTime;
            appt.EndTime = endTime;
            appt.StoreDecision = DecisionStatus.Pending; // Store 5dk i├ğinde onay verecek
            // FreeBarberDecision hala Pending (30dk i├ğinde red edebilir)
            // CustomerDecision hala null (Store onaylad─▒ktan sonra Pending olacak)
            appt.UpdatedAt = DateTime.UtcNow;

            // Manuel barber kontrol├╝
            appt.ManuelBarberId = chair.ManuelBarberId;

            await appointmentDal.Update(appt);
            await ReplaceAppointmentServiceOfferingsAsync(appt.Id, serviceOfferingIds);

            await UpdateThreadStoreOwnerAsync(appt.Id, appt.BarberStoreUserId);

            // Thread'i g├╝ncelle (3'l├╝ thread olacak)
            await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);

            // D├╝kkana bildirim g├Ânder (sadece d├╝kkan, m├╝┼şteriye g├Ânderme)
            if (appt.BarberStoreUserId.HasValue)
            {
                await notifySvc.NotifyWithAppointmentToRecipientsAsync(
                    appt,
                    NotificationType.AppointmentCreated,
                    new[] { appt.BarberStoreUserId.Value },
                    actorUserId: freeBarberUserId);
            }

            // Notification payload update
            await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                appt.Id,
                appt.Status,
                appt.StoreDecision,
                appt.FreeBarberDecision,
                appt.CustomerDecision,
                appt.PendingExpiresAt
            );

            // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Badge update
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- DECISIONS (STORE / FREEBARBER) ----------------
        [TransactionScopeAspect]

        public async Task<IDataResult<bool>> StoreDecisionAsync(Guid storeOwnerUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);
            if (appt.BarberStoreUserId != storeOwnerUserId) return new ErrorDataResult<bool>(false, Messages.Unauthorized);
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>(false, Messages.AppointmentNotPendingStatus);
            
            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;
            
            var isStoreSelectionFlow = appt.StoreSelectionType == StoreSelectionType.StoreSelection &&
                appt.CustomerUserId.HasValue &&
                appt.FreeBarberUserId.HasValue;

            if (isStoreSelectionFlow)
            {
                // StoreDecision null veya Pending olmal─▒
                if (appt.StoreDecision.HasValue && appt.StoreDecision.Value != DecisionStatus.Pending)
                    return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);

                appt.StoreDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
                appt.UpdatedAt = DateTime.UtcNow;

                if (!approve)
                {
                    ClearStoreSelectionSlot(appt);
                    SetStoreSelectionOverallExpiry(appt);
                }
                else
                {
                    appt.CustomerDecision = DecisionStatus.Pending;
                    SetStoreSelectionStepExpiry(appt);
                }

                await appointmentDal.Update(appt);

                if (!approve)
                {
                    await UpdateThreadStoreOwnerAsync(appt.Id, null);
                }

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                    appt.Id,
                    appt.Status,
                    appt.StoreDecision,
                    appt.FreeBarberDecision,
                    appt.CustomerDecision,
                    appt.PendingExpiresAt
                );

                if (!approve)
                {
                    if (appt.FreeBarberUserId.HasValue)
                    {
                        await notifySvc.NotifyToRecipientsAsync(
                            appt.Id,
                            NotificationType.StoreRejectedSelection,
                            new[] { appt.FreeBarberUserId.Value },
                            actorUserId: storeOwnerUserId);
                    }
                    else
                    {
                        await notifySvc.NotifyAsync(appt.Id, NotificationType.StoreRejectedSelection, actorUserId: storeOwnerUserId);
                    }
                }
                else
                {
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.StoreApprovedSelection, actorUserId: storeOwnerUserId);
                }

                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
            }

            // ekstra: ayn─▒ taraf tekrar karar veremesin (null veya Pending olmal─▒)
            if (appt.StoreDecision.HasValue && appt.StoreDecision.Value != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);

            appt.StoreDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                // Customer -> FreeBarber + Store senaryosunda reddetme
                if (appt.CustomerUserId.HasValue && appt.FreeBarberUserId.HasValue)
                {
                    // Thread'den d├╝kkan ├ğ─▒kar─▒lacak, koltuk m├╝sait olacak
                    ClearStoreSelectionSlot(appt);
                    appt.StoreDecision = DecisionStatus.Rejected;
                    // Status hala Pending kalacak, free barber tekrar d├╝kkan arayabilir
                }
                else
                {
                    appt.Status = AppointmentStatus.Rejected;
                    appt.PendingExpiresAt = null;
                }
            }
            else
            {
                // Customer -> FreeBarber + Store senaryosu
                if (appt.CustomerUserId.HasValue && appt.FreeBarberUserId.HasValue)
                {
                    // D├╝kkan onaylad─▒, ┼şimdi m├╝┼şteri onay─▒ bekleniyor
                    // Status hala Pending kalacak, CustomerDecision bekleniyor
                    appt.CustomerDecision = DecisionStatus.Pending;
                    // M├╝┼şteri onay─▒ i├ğin 30 dakikal─▒k toplam s├╝re devam ediyor (yeni s├╝re eklenmez)
                    SetStoreSelectionOverallExpiry(appt);
                    
                    // AppointmentDecisionUpdated bildirimleri kald─▒r─▒ld─▒ - kullan─▒c─▒ iste─şi
                }
                // Normal senaryo: freebarber veya customer yoksa direkt Approved olur
                else if (!appt.CustomerUserId.HasValue || !appt.FreeBarberUserId.HasValue)

                {

                    appt.Status = AppointmentStatus.Approved;

                    appt.ApprovedAt = DateTime.UtcNow;

                    appt.PendingExpiresAt = null;

                }

                else if (appt.FreeBarberDecision == DecisionStatus.Approved)
                {
                    appt.Status = AppointmentStatus.Approved;
                    appt.ApprovedAt = DateTime.UtcNow;
                    appt.PendingExpiresAt = null;
                }
            }

            await appointmentDal.Update(appt);

            // Decision verildikten sonra notification payload'lar─▒n─▒ g├╝ncelle (status, decisions)
            // Bu sayede frontend'de butonlar do─şru ┼şekilde gizlenir
            await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                appt.Id,
                appt.Status,
                appt.StoreDecision,
                appt.FreeBarberDecision,
                appt.CustomerDecision,
                appt.PendingExpiresAt
            );

            // ├ûNEML─░: Decision ba┼şar─▒l─▒ ise (Approved) notification'lar─▒ read yap
            // Rejected durumunda read yap─▒lmamal─▒ (kullan─▒c─▒ g├Ârmeli)
            if (appt.Status == AppointmentStatus.Approved)
            {
                var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                // Actor (karar veren ki┼şi - storeOwnerUserId) hari├ğ di─şer kullan─▒c─▒lar─▒n notification'lar─▒n─▒ read yap
                foreach (var userId in participantUserIds)
                {
                    if (userId != storeOwnerUserId)
                    {
                        await notificationService.MarkReadByAppointmentIdAsync(userId, appt.Id);
                    }
                }
            }

            if (appt.Status == AppointmentStatus.Rejected)
            {
                await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: storeOwnerUserId);

                // Rejected durumunda chat mesaj─▒ g├Ânder
                try
                {
                    var rejectionMessage = "Randevu talebiniz reddedildi.";
                    if (appt.CustomerUserId.HasValue)
                    {
                        // await chatService.SendMessageAsync(storeOwnerUserId, appt.Id, rejectionMessage);
                    }
                }
                catch
                {
                    // Chat mesaj─▒ g├Ânderilemezse devam et, kritik de─şil
                }

                await UpdateThreadOnAppointmentStatusChangeAsync(appt);

                // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
            }

            if (appt.Status == AppointmentStatus.Approved)
            {
                // Approved durumunda serbest berberi me┼şgul yap (e─şer varsa ve zaten me┼şgul de─şilse)
                if (appt.FreeBarberUserId.HasValue)
                {
                    var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                    if (fb is not null && fb.IsAvailable)
                    {
                        await SetFreeBarberAvailabilityAsync(fb, false);
                    }
                }
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: storeOwnerUserId);

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);

                // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
            }

            // AppointmentDecisionUpdated bildirimleri kald─▒r─▒ld─▒ - kullan─▒c─▒ iste─şi

            // Decision g├╝ncellendi─şinde ilgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Decision g├╝ncellendi─şinde chat mesaj─▒ g├Ânder
            try
            {
                var decisionMessage = approve ? "Randevu talebiniz kabul edildi. Di─şer taraf─▒n onay─▒ bekleniyor." : "Randevu talebiniz reddedildi.";
                if (appt.CustomerUserId.HasValue)
                {
                    // await chatService.SendMessageAsync(storeOwnerUserId, appt.Id, decisionMessage);
                }
            }
            catch
            {
                // Chat mesaj─▒ g├Ânderilemezse devam et, kritik de─şil
            }

            // Transaction commit sonras─▒ badge update'leri ├ğal─▒┼şt─▒r
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }
        [TransactionScopeAspect]

        public async Task<IDataResult<bool>> FreeBarberDecisionAsync(Guid freeBarberUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(Messages.AppointmentNotFound);
            if (appt.FreeBarberUserId != freeBarberUserId) return new ErrorDataResult<bool>(Messages.Unauthorized);
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>(Messages.AppointmentNotPending);

            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;

            // 3'l├╝ sistemde (StoreSelection): FreeBarber t├╝m randevu Approved olana kadar ve 30dk dolmadan red edebilir
            var isStoreSelectionFlow = appt.StoreSelectionType == StoreSelectionType.StoreSelection && 
                                      appt.CustomerUserId.HasValue;
            
            if (isStoreSelectionFlow)
            {
                // 30 dakikal─▒k toplam s├╝re kontrol├╝
                var now = DateTime.UtcNow;
                var overallExpiresAt = appt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                if (approve)
                    return new ErrorDataResult<bool>(false, "Bu randevuda serbest berber onay ad─▒m─▒ yok. D├╝kkan se├ğimi bekleniyor.");

                // M├╝┼şteri onay verdiyse art─▒k free barber reddedemez
                if (appt.CustomerDecision == DecisionStatus.Approved)
                    return new ErrorDataResult<bool>(false, "M├╝┼şteri onay verdi─şi i├ğin bu randevu art─▒k reddedilemez.");
                
                // Randevu tamam─▒ Approved olduysa red edemez
                if (appt.Status == AppointmentStatus.Approved)
                    return new ErrorDataResult<bool>(false, "Randevu onayland─▒, art─▒k red edemezsiniz.");
                
                // 30 dakika dolmad─▒ysa red edebilir (FreeBarberDecision durumuna bakmadan)
                if (now > overallExpiresAt)
                    return new ErrorDataResult<bool>(false, "Reddetme s├╝resi doldu.");
            }
            else
            {
                // Di─şer senaryolarda: FreeBarberDecision null veya Pending olmal─▒
                if (appt.FreeBarberDecision.HasValue && appt.FreeBarberDecision.Value != DecisionStatus.Pending)
                    return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);
            }

            appt.FreeBarberDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                // FreeBarber reddetti
                
                // StoreSelection (D├╝kkan Se├ğ) senaryosu: M├╝┼şteriden gelen ilk istek
                if (appt.StoreSelectionType == StoreSelectionType.StoreSelection && 
                    appt.CustomerUserId.HasValue)
                {
                    // 30 dakikal─▒k s├╝re dolmad─▒─ş─▒n─▒ kontrol et (opsiyonel g├╝venlik kontrol├╝)
                    var now = DateTime.UtcNow;
                    var overallExpiresAt = appt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                    if (now > overallExpiresAt)
                        return new ErrorDataResult<bool>(false, "Reddetme s├╝resi doldu.");
                    
                    appt.Status = AppointmentStatus.Rejected;
                    appt.PendingExpiresAt = null;
                    
                    // E─şer d├╝kkan se├ğilmi┼şse temizle
                    if (appt.BarberStoreUserId.HasValue)
                    {
                        ClearStoreSelectionSchedule(appt);
                        await UpdateThreadStoreOwnerAsync(appt.Id, null);
                        await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    }
                    
                    await appointmentDal.Update(appt);
                    
                    // FreeBarber'─▒ m├╝sait yap
                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    
                    // Thread'i pasif yap
                    await UpdateThreadOnAppointmentStatusChangeAsync(appt);
                    
                    // Notification payload g├╝ncelle
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id,
                        appt.Status,
                        appt.StoreDecision,
                        appt.FreeBarberDecision,
                        appt.CustomerDecision,
                        appt.PendingExpiresAt
                    );
                    
                    // M├╝┼şteri'ye ├Âzel bildirim: FreeBarberRejectedInitial
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.FreeBarberRejectedInitial, actorUserId: freeBarberUserId);
                    
                    // SignalR ile bildir
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
                }
                
                // Di─şer senaryolar (CustomRequest, Store -> FreeBarber, vs.)
                appt.Status = AppointmentStatus.Rejected;
                appt.PendingExpiresAt = null;

                // Customer -> FreeBarber + Store senaryosunda FreeBarber reddederse
                if (appt.CustomerUserId.HasValue && appt.BarberStoreUserId.HasValue)
                {
                    // D├╝kkan thread'den ├ğ─▒kar─▒lacak, koltuk m├╝sait olacak
                    ClearStoreSelectionSchedule(appt);
                    await UpdateThreadStoreOwnerAsync(appt.Id, null);
                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    
                    // 3'l├╝ sistemde FreeBarber d├╝kkandan sonra reddetti
                    await appointmentDal.Update(appt);
                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    await UpdateThreadOnAppointmentStatusChangeAsync(appt);
                    
                    // Notification payload g├╝ncelle
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id,
                        appt.Status,
                        appt.StoreDecision,
                        appt.FreeBarberDecision,
                        appt.CustomerDecision,
                        appt.PendingExpiresAt
                    );
                    
                    // M├╝┼şteri ve Store'a bildir
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.FreeBarberRejectedInitial, actorUserId: freeBarberUserId);
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
                }
            }
            else
            {
                // FreeBarber onaylad─▒
                
                // Customer -> FreeBarber randevusu
                if (appt.CustomerUserId.HasValue && appt.BarberStoreUserId == null)
                {
                    // ─░ste─şime G├Âre (CustomRequest) senaryosu: FreeBarber onaylad─▒, ┼şimdi Customer onay─▒ bekleniyor
                    if (appt.StoreSelectionType == StoreSelectionType.CustomRequest)
                    {
                        // Status hala Pending, CustomerDecision bekleniyor
                        appt.CustomerDecision = DecisionStatus.Pending;
                        // FreeBarberDecision zaten Approved olarak set edildi (sat─▒r 798)
                    }
                    // D├╝kkan Se├ğ senaryosunda: FreeBarber onaylad─▒ktan sonra d├╝kkan arayacak
                    // Bu durumda FreeBarberDecision Pending kal─▒r (randevu sonuna kadar)
                    // StoreSelection logic AddStoreToExistingAppointmentAsync'te
                }
                // Customer -> FreeBarber + Store senaryosu
                                else if (appt.CustomerUserId.HasValue && appt.BarberStoreUserId.HasValue)
                {
                    // Dükkan Seç senaryosu: Store onayı bekleniyor
                    if (appt.StoreDecision == DecisionStatus.Approved)
                    {
                        // Store zaten onaylamış, şimdi Customer onayı bekleniyor
                        // Status hala Pending kalacak, CustomerDecision bekleniyor
                    }
                    else if (appt.StoreDecision == DecisionStatus.Pending)
                    {
                        // Store henüz karar vermemiş, FreeBarber onayladı ama Store onayı bekleniyor
                        // Status hala Pending kalacak
                    }
                }
                else if (!appt.CustomerUserId.HasValue && appt.BarberStoreUserId.HasValue)
                {
                    appt.Status = AppointmentStatus.Approved;
                    appt.ApprovedAt = DateTime.UtcNow;
                    appt.PendingExpiresAt = null;
                }
            }

            await appointmentDal.Update(appt);

            // Decision verildikten sonra notification payload'lar─▒n─▒ g├╝ncelle (status, decisions)
            // Bu sayede frontend'de butonlar do─şru ┼şekilde gizlenir
            await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                appt.Id,
                appt.Status,
                appt.StoreDecision,
                appt.FreeBarberDecision,
                appt.CustomerDecision,
                appt.PendingExpiresAt
            );

            // ├ûNEML─░: Decision ba┼şar─▒l─▒ ise (Approved) notification'lar─▒ read yap
            // Rejected durumunda read yap─▒lmamal─▒ (kullan─▒c─▒ g├Ârmeli)
            if (appt.Status == AppointmentStatus.Approved)
            {
                var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                // Actor (karar veren ki┼şi - freeBarberUserId) hari├ğ di─şer kullan─▒c─▒lar─▒n notification'lar─▒n─▒ read yap
                foreach (var userId in participantUserIds)
                {
                    if (userId != freeBarberUserId)
                    {
                        await notificationService.MarkReadByAppointmentIdAsync(userId, appt.Id);
                    }
                }
            }

            if (appt.Status == AppointmentStatus.Rejected)
            {
                await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: freeBarberUserId);

                // Rejected durumunda chat mesaj─▒ g├Ânder
                try
                {
                    var rejectionMessage = "Randevu talebiniz reddedildi.";
                    if (appt.CustomerUserId.HasValue)
                    {
                        // await chatService.SendMessageAsync(freeBarberUserId, appt.Id, rejectionMessage);
                    }
                    else if (appt.BarberStoreUserId.HasValue)
                    {
                        // await chatService.SendMessageAsync(freeBarberUserId, appt.Id, rejectionMessage);
                    }
                }
                catch
                {
                    // Chat mesaj─▒ g├Ânderilemezse devam et, kritik de─şil
                }

                await UpdateThreadOnAppointmentStatusChangeAsync(appt);

                // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
            }

            if (appt.Status == AppointmentStatus.Approved)
            {
                // Approved durumunda serbest berberi me┼şgul yap (e─şer zaten me┼şgul de─şilse)
                var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
                if (fb is not null && fb.IsAvailable)
                {
                    await SetFreeBarberAvailabilityAsync(fb, false);
                }
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: freeBarberUserId);

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);

                // Decision sonras─▒ chat mesaj─▒ g├Ânder
                try
                {
                    var decisionMessage = approve ? "Randevu talebiniz kabul edildi." : "Randevu talebiniz reddedildi.";
                    if (appt.CustomerUserId.HasValue)
                    {
                        // await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                    }
                    else if (appt.BarberStoreUserId.HasValue)
                    {
                        // await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                    }
                }
                catch
                {
                    // Chat mesaj─▒ g├Ânderilemezse devam et, kritik de─şil
                }

                // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir (aktif tab'da g├Âr├╝nmesi i├ğin)
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
            }

            // AppointmentDecisionUpdated bildirimleri kald─▒r─▒ld─▒ - kullan─▒c─▒ iste─şi

            // Decision g├╝ncellendi─şinde chat mesaj─▒ g├Ânder
            try
            {
                var decisionMessage = approve ? "Randevu talebiniz kabul edildi. Di─şer taraf─▒n onay─▒ bekleniyor." : "Randevu talebiniz reddedildi.";
                if (appt.CustomerUserId.HasValue)
                {
                    // await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                }
                else if (appt.BarberStoreUserId.HasValue)
                {
                    // await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                }
            }
            catch
            {
                // Chat mesaj─▒ g├Ânderilemezse devam et, kritik de─şil
            }

            // Decision g├╝ncellendi─şinde ilgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonras─▒ badge update'leri ├ğal─▒┼şt─▒r
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- CUSTOMER DECISION (NEW) ----------------

        /// <summary>
        /// M├╝┼şteri karar─▒ - Customer -> FreeBarber + Store senaryosunda m├╝┼şteri onay─▒
        /// </summary>
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> CustomerDecisionAsync(Guid customerUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);
            if (appt.CustomerUserId != customerUserId) return new ErrorDataResult<bool>(false, Messages.Unauthorized);
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>(false, Messages.AppointmentNotPendingStatus);

            // ─░ki senaryo var:
            // 1. Customer -> FreeBarber (─░ste─şime G├Âre - CustomRequest): Store yok, FreeBarber onaylam─▒┼ş olmal─▒
            // 2. Customer -> FreeBarber + Store (D├╝kkan Se├ğ - StoreSelection): Store ve FreeBarber var, Store onaylam─▒┼ş olmal─▒

            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;

            // CustomerDecision null veya Pending olmal─▒
            if (appt.CustomerDecision.HasValue && appt.CustomerDecision.Value != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);

            // CustomRequest (─░ste─şime G├Âre) senaryosu
            if (appt.StoreSelectionType == StoreSelectionType.CustomRequest && 
                appt.FreeBarberUserId.HasValue && 
                !appt.BarberStoreUserId.HasValue)
            {
                // FreeBarber onaylam─▒┼ş olmal─▒
                if (appt.FreeBarberDecision != DecisionStatus.Approved)
                    return new ErrorDataResult<bool>(false, "Serbest berber onay─▒ bekleniyor.");

                appt.CustomerDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
                appt.UpdatedAt = DateTime.UtcNow;

                if (!approve)
                {
                    // M├╝┼şteri reddetti
                    appt.Status = AppointmentStatus.Rejected;
                    appt.PendingExpiresAt = null;
                    
                    await appointmentDal.Update(appt);
                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    await UpdateThreadOnAppointmentStatusChangeAsync(appt);
                    
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id, appt.Status, appt.StoreDecision, appt.FreeBarberDecision, 
                        appt.CustomerDecision, appt.PendingExpiresAt);
                    
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: customerUserId);
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
                }
                else
                {
                    // M├╝┼şteri onaylad─▒ - randevu Approved
                    appt.Status = AppointmentStatus.Approved;
                    appt.ApprovedAt = DateTime.UtcNow;
                    appt.PendingExpiresAt = null;
                    
                    await appointmentDal.Update(appt);
                    
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id, appt.Status, appt.StoreDecision, appt.FreeBarberDecision, 
                        appt.CustomerDecision, appt.PendingExpiresAt);
                    
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: customerUserId);

                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                return new SuccessDataResult<bool>(true);
                }
            }

            // StoreSelection (D├╝kkan Se├ğ) senaryosu - 3'l├╝ sistem
            if (!appt.FreeBarberUserId.HasValue || !appt.BarberStoreUserId.HasValue)
                return new ErrorDataResult<bool>(false, "Bu randevu i├ğin m├╝┼şteri karar─▒ verilemez.");

            // Store onaylam─▒┼ş olmal─▒
            if (appt.StoreDecision != DecisionStatus.Approved)
                return new ErrorDataResult<bool>(false, "D├╝kkan onay─▒ bekleniyor.");

            appt.CustomerDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                await notifySvc.NotifyAsync(appt.Id, NotificationType.CustomerRejectedFinal, actorUserId: customerUserId);

                ClearStoreSelectionSlot(appt);
                SetStoreSelectionOverallExpiry(appt);
                // M├╝┼şteri reddetti - d├╝kkan thread'den ├ğ─▒kar─▒lacak, koltuk m├╝sait olacak
                appt.StoreDecision = DecisionStatus.Pending; // D├╝kkan tekrar se├ğilebilir
                appt.CustomerDecision = null; // CustomerDecision null'a ├ğekilir
                // Status hala Pending kalacak, free barber tekrar d├╝kkan arayabilir
            }
            else
            {
                // M├╝┼şteri onaylad─▒ - randevu Approved olur
                appt.Status = AppointmentStatus.Approved;
                appt.ApprovedAt = DateTime.UtcNow;
                appt.PendingExpiresAt = null;
                
                // FreeBarberDecision art─▒k Approved olur (randevu onayland─▒─ş─▒nda)
                appt.FreeBarberDecision = DecisionStatus.Approved;

                // FreeBarber ve Store'a bildirim
                await notifySvc.NotifyAsync(appt.Id, NotificationType.CustomerApprovedFinal, actorUserId: customerUserId);

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
            }

            await appointmentDal.Update(appt);

            if (!approve)
            {
                await UpdateThreadStoreOwnerAsync(appt.Id, null);
                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
            }
            // Notification payload update
            await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                appt.Id,
                appt.Status,
                appt.StoreDecision,
                appt.FreeBarberDecision,
                appt.CustomerDecision,
                appt.PendingExpiresAt
            );

            // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Badge update
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- CANCEL / COMPLETE ----------------
        [TransactionScopeAspect]

        public async Task<IDataResult<bool>> CancelAsync(Guid userId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);

            var isParticipant =
                appt.CustomerUserId == userId ||
                appt.FreeBarberUserId == userId ||
                appt.BarberStoreUserId == userId;

            if (!isParticipant) return new ErrorDataResult<bool>(false, Messages.Unauthorized);

            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<bool>(false, Messages.AppointmentCannotBeCancelled);

            // ─░ptal kurallar─▒:
            // 1. M├╝┼şteri: Sadece Approved durumunda iptal edebilir (Pending'de edemez - ├ğ├╝nk├╝ talebi o g├Ânderdi)
            // 2. FreeBarber: Hem Pending hem Approved durumunda iptal edebilir
            // 3. Store: Hem Pending hem Approved durumunda iptal edebilir
            
            if (appt.CustomerUserId == userId && appt.Status == AppointmentStatus.Approved)
            {
                return new ErrorDataResult<bool>(false, "Onaylanan randevuyu m├╝┼şteri iptal edemez.");
            }

            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledByUserId = userId;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            // FreeBarber m├╝saitli─şini serbest b─▒rak
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            // Koltuk m├╝saitli─şini serbest b─▒rak (e─şer varsa)
            // Not: Koltuk otomatik olarak m├╝sait olacak ├ğ├╝nk├╝ status Cancelled oldu
            // GetAvailability sorgusu sadece Pending ve Approved randevular─▒ kontrol ediyor

            // ─░ptal edildi─şinde iptal eden ki┼şi hari├ğ di─şer t├╝m taraflara bildirim g├Ânder
            // notifySvc.NotifyAsync zaten actorUserId hari├ğ t├╝m kat─▒l─▒mc─▒lara bildirim g├Ânderiyor
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCancelled, actorUserId: userId);

            // ─░ptal durumunda chat mesaj─▒ g├Ânder
            try
            {
                // ─░ptal eden ki┼şinin ad─▒n─▒ belirle
                string cancellerName = "Bir kullan─▒c─▒";
                if (appt.CustomerUserId == userId)
                {
                    var customer = await userDal.Get(x => x.Id == userId);
                    if (customer != null)
                        cancellerName = $"{customer.FirstName} {customer.LastName}";
                }
                else if (appt.FreeBarberUserId == userId)
                {
                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                    if (freeBarber != null)
                        cancellerName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                }
                else if (appt.BarberStoreUserId == userId)
                {
                    var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == userId);
                    if (store != null)
                        cancellerName = store.StoreName;
                }

                var cancelMessage = $"{cancellerName} randevuyu iptal etti.";
                // await chatService.SendMessageAsync(userId, appt.Id, cancelMessage);
            }
            catch
            {
                // Chat mesaj─▒ g├Ânderilemezse devam et, kritik de─şil
            }

            // Thread g├╝ncellemesi (thread kald─▒r─▒lacak)
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);

            // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonras─▒ badge update'leri ├ğal─▒┼şt─▒r
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }
        [TransactionScopeAspect]

        public async Task<IDataResult<bool>> CompleteAsync(Guid userId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(Messages.AppointmentNotFound);

            // Customer -> FreeBarber (─░ste─şime G├Âre) senaryosunda free barber tamamlayabilir
            bool canComplete = false;
            if (appt.CustomerUserId.HasValue && appt.FreeBarberUserId.HasValue && appt.BarberStoreUserId == null)
            {
                // ─░ste─şime G├Âre senaryosu - free barber tamamlayabilir
                canComplete = appt.FreeBarberUserId == userId;
            }
            else if (appt.BarberStoreUserId.HasValue)
            {
                // Normal senaryo - sadece store owner tamamlayabilir
                canComplete = appt.BarberStoreUserId == userId;
            }

            if (!canComplete) return new ErrorDataResult<bool>(Messages.Unauthorized);

            if (appt.Status != AppointmentStatus.Approved) return new ErrorDataResult<bool>(Messages.AppointmentNotApproved);

            // ─░ste─şe G├Âre randevularda (CustomRequest ve store yok) tarih/saat kontrol├╝ yapma
            // Bu randevularda AppointmentDate ve StartTime/EndTime null olabilir
            var isCustomRequestWithoutStore = appt.StoreSelectionType.HasValue &&
                appt.StoreSelectionType.Value == StoreSelectionType.CustomRequest &&
                appt.CustomerUserId.HasValue &&
                appt.FreeBarberUserId.HasValue &&
                !appt.BarberStoreUserId.HasValue;

            // Normal randevularda (d├╝kkan dahil) tarih/saat kontrol├╝ yap
            var hasSchedule = appt.AppointmentDate.HasValue && appt.StartTime.HasValue && appt.EndTime.HasValue;
            if (!isCustomRequestWithoutStore && hasSchedule)
            {
                // TR saati ile randevu ba┼şlang─▒├ğ ve biti┼ş tarihlerini kontrol et
                var startTrRes = GetAppointmentStartTr(appt);
                if (!startTrRes.Success) return new ErrorDataResult<bool>(startTrRes.Message);

                var endTrRes = GetAppointmentEndTr(appt);
                if (!endTrRes.Success) return new ErrorDataResult<bool>(endTrRes.Message);

                var nowTr = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

                // Randevu ba┼şlang─▒├ğ tarihi ge├ğmi┼ş olmal─▒ (randevu ba┼şlam─▒┼ş olmal─▒)
                if (nowTr < startTrRes.Data)
                    return new ErrorDataResult<bool>(Messages.AppointmentTimeNotPassed);

                // Randevu biti┼ş tarihi ge├ğmi┼ş olmal─▒ (randevu bitmi┼ş olmal─▒)
                if (nowTr < endTrRes.Data)
                    return new ErrorDataResult<bool>(Messages.AppointmentTimeNotPassed);
            }

            appt.Status = AppointmentStatus.Completed;
            appt.CompletedAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            // FreeBarber m├╝saitli─şini serbest b─▒rak
            // Completed durumunda serbest berberi m├╝sait yap
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCompleted, actorUserId: userId);

            // Tamamlanma durumunda chat mesaj─▒ g├Ânder
            try
            {
                var completeMessage = "Randevu tamamland─▒.";
                // await chatService.SendMessageAsync(userId, appt.Id, completeMessage);
            }
            catch
            {
                // Chat mesaj─▒ g├Ânderilemezse devam et, kritik de─şil
            }

            // Thread g├╝ncellemesi (thread kald─▒r─▒lacak)
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);

            // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonras─▒ badge update'leri ├ğal─▒┼şt─▒r
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- RULES / HELPERS ----------------

        /// <summary>
        /// Creates appointment service offerings snapshot from service offering IDs.
        /// Extracted to reduce code duplication across create appointment methods.
        /// </summary>
        private async Task CreateAppointmentServiceOfferingsAsync(Guid appointmentId, List<Guid>? serviceOfferingIds)
        {
            if (serviceOfferingIds == null || serviceOfferingIds.Count == 0)
                return;

            var offs = await offeringDal.GetServiceOfferingsByIdsAsync(serviceOfferingIds);
            var appointmentServiceOfferings = offs.Select(o => new AppointmentServiceOffering
            {
                Id = Guid.NewGuid(),
                AppointmentId = appointmentId,
                ServiceOfferingId = o.Id,
                ServiceName = o.ServiceName,
                Price = o.Price
            }).ToList();

            // AddRange ile toplu ekleme - performans i├ğin daha iyi
            if (appointmentServiceOfferings.Any())
            {
                await apptOfferingDal.AddRange(appointmentServiceOfferings);
            }
        }

        private async Task ReplaceAppointmentServiceOfferingsAsync(Guid appointmentId, List<Guid>? serviceOfferingIds)
        {
            var existing = await apptOfferingDal.GetAll(x => x.AppointmentId == appointmentId);
            if (existing != null && existing.Count > 0)
            {
                await apptOfferingDal.DeleteAll(existing);
            }

            await CreateAppointmentServiceOfferingsAsync(appointmentId, serviceOfferingIds);
        }

        private async Task<IResult> EnsureServiceOfferingsBelongToOwnerAsync(List<Guid>? serviceOfferingIds, Guid ownerId)
        {
            if (serviceOfferingIds == null || serviceOfferingIds.Count == 0)
                return new ErrorResult(Messages.ServiceOfferingRequired);

            var offerings = await offeringDal.GetAll(o => serviceOfferingIds.Contains(o.Id));
            if (offerings.Count != serviceOfferingIds.Count)
                return new ErrorResult(Messages.ServiceOfferingOwnerMismatch);

            if (offerings.Any(o => o.OwnerId != ownerId))
                return new ErrorResult(Messages.ServiceOfferingOwnerMismatch);

            return new SuccessResult();
        }

        private static void ClearStoreSelectionSlot(Appointment appt)
        {
            appt.BarberStoreUserId = null;
            appt.ChairId = null;
            appt.AppointmentDate = null;
            appt.StartTime = null;
            appt.EndTime = null;
            appt.ManuelBarberId = null;
        }

        private static void ClearStoreSelectionSchedule(Appointment appt)
        {
            appt.ChairId = null;
            appt.AppointmentDate = null;
            appt.StartTime = null;
            appt.EndTime = null;
            appt.ManuelBarberId = null;
        }

        private DateTime GetStoreSelectionOverallExpiry(Appointment appt)
        {
            return appt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
        }

        private void SetStoreSelectionOverallExpiry(Appointment appt)
        {
            appt.PendingExpiresAt = GetStoreSelectionOverallExpiry(appt);
        }

        private void SetStoreSelectionStepExpiry(Appointment appt)
        {
            var overall = GetStoreSelectionOverallExpiry(appt);
            var step = DateTime.UtcNow.AddMinutes(StoreSelectionStepMinutes);
            appt.PendingExpiresAt = step <= overall ? step : overall;
        }

        private async Task UpdateThreadStoreOwnerAsync(Guid appointmentId, Guid? storeOwnerUserId)
        {
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread == null) return;

            thread.StoreOwnerUserId = storeOwnerUserId;
            thread.UpdatedAt = DateTime.UtcNow;
            await threadDal.Update(thread);
        }

        private async Task<IResult> EnsureChairNoOverlapAsync(Guid chairId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            // ├ûNEML─░: Unique index t├╝m status'leri kontrol ediyor (ChairId, AppointmentDate, StartTime, EndTime)
            // Bu y├╝zden ayn─▒ slot'ta herhangi bir status'te randevu varsa (Pending, Approved, Cancelled, Rejected, Completed, Unanswered)
            // yeni randevu olu┼şturulamaz
            // Ancak mant─▒ken sadece Pending ve Approved randevular slot'u dolu tutmal─▒
            // Di─şer status'ler (Cancelled, Rejected, Completed, Unanswered) slot'u bo┼şaltmal─▒

            // ├ûnce mant─▒ksal overlap kontrol├╝: Sadece Pending ve Approved randevular slot'u dolu tutar
            var hasActiveOverlap = await appointmentDal.AnyAsync(x =>
                x.ChairId == chairId &&
                x.AppointmentDate == date &&
                (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved) &&
                x.StartTime < end &&
                x.EndTime > start);

            if (hasActiveOverlap)
                return new ErrorResult(Messages.AppointmentSlotOverlap);

            // NOTE: Unique index (ChairId, AppointmentDate, StartTime, EndTime) zaten var
            // Bu index ayn─▒ slot'ta herhangi bir randevu olu┼şturulmas─▒n─▒ engeller
            // Exact match kontrol├╝ gereksiz ├ğ├╝nk├╝ unique constraint zaten bunu yap─▒yor
            // E─şer exact match varsa, Add() ├ğa─şr─▒s─▒nda DbUpdateException f─▒rlat─▒lacak
            // ve catch blo─şunda yakalanacak (sat─▒r 177, 298, 402)

            return new SuccessResult();
        }

        private async Task<IResult> EnsureStoreIsOpenAsync(Guid storeId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            var dow = date.DayOfWeek;

            var wh = await workingHourDal.Get(x =>
                x.OwnerId == storeId &&
                x.DayOfWeek == dow);

            if (wh is null)
                return new ErrorResult(Messages.StoreNoWorkingHours);

            if (wh.IsClosed)
                return new ErrorResult(Messages.StoreClosed);

            if (wh.StartTime > start || wh.EndTime < end)
                return new ErrorResult(Messages.StoreNotOpen);

            return new SuccessResult();
        }

        private async Task<IResult> EnsureStoreIsOpenNowAsync(Guid storeId)
        {
            var now = DateTime.Now;
            var dow = now.DayOfWeek;
            var currentTime = now.TimeOfDay;

            var wh = await workingHourDal.Get(x =>
                x.OwnerId == storeId &&
                x.DayOfWeek == dow);

            if (wh is null)
                return new ErrorResult(Messages.StoreNoWorkingHours);

            if (wh.IsClosed)
                return new ErrorResult(Messages.StoreClosed);

            if (wh.StartTime > currentTime || wh.EndTime < currentTime)
                return new ErrorResult("D├╝kkan ┼şu an kapal─▒. L├╝tfen mesai saatleri i├ğinde randevu olu┼şturun.");

            return new SuccessResult();
        }

        private IDataResult<DateTime> GetAppointmentStartTr(Appointment appt)
        {
            try
            {
                if (!appt.AppointmentDate.HasValue || !appt.StartTime.HasValue)
                    return new ErrorDataResult<DateTime>(Messages.AppointmentEndTimeCalculationFailed);

                // DateOnly + TimeSpan -> DateTime (TR local kabul)
                var startLocal = appt.AppointmentDate.Value.ToDateTime(TimeOnly.FromTimeSpan(appt.StartTime.Value));

                // local time (TR) olarak DateTime d├Ând├╝r├╝yoruz
                // (DateTime.Now ile k─▒yas i├ğin)
                return new SuccessDataResult<DateTime>(startLocal);
            }
            catch
            {
                return new ErrorDataResult<DateTime>(Messages.AppointmentEndTimeCalculationFailed);
            }
        }

        private IDataResult<DateTime> GetAppointmentEndTr(Appointment appt)
        {
            try
            {
                if (!appt.AppointmentDate.HasValue || !appt.EndTime.HasValue)
                    return new ErrorDataResult<DateTime>(Messages.AppointmentEndTimeCalculationFailed);

                // DateOnly + TimeSpan -> DateTime (TR local kabul)
                var endLocal = appt.AppointmentDate.Value.ToDateTime(TimeOnly.FromTimeSpan(appt.EndTime.Value));

                // local time (TR) olarak DateTime d├Ând├╝r├╝yoruz
                // (DateTime.Now ile k─▒yas i├ğin)
                return new SuccessDataResult<DateTime>(endLocal);
            }
            catch
            {
                return new ErrorDataResult<DateTime>(Messages.AppointmentEndTimeCalculationFailed);
            }
        }

        private async Task ReleaseFreeBarberIfNeededAsync(Guid? freeBarberUserId)
        {
            if (!freeBarberUserId.HasValue) return;

            // FreeBarber entity'sini al ve overload metodunu kullan (daha verimli)
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId.Value);
            if (fb is null) return;

            await SetFreeBarberAvailabilityAsync(fb, true);
        }

        /// <summary>
        /// Ortak appointment olu┼şturma i┼şlemleri (service offerings, thread, notification, badge update)
        /// </summary>
        private async Task FinalizeAppointmentCreationAsync(Appointment appt, List<Guid>? serviceOfferingIds, Guid actorUserId)
        {
            // Service offerings snapshot
            await CreateAppointmentServiceOfferingsAsync(appt.Id, serviceOfferingIds);

            // Thread olu┼ştur ve push et
            await EnsureThreadAndPushCreatedAsync(appt);

            // Notification g├Ânder
            await notifySvc.NotifyWithAppointmentAsync(appt, NotificationType.AppointmentCreated, actorUserId: actorUserId);

            // Badge update
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();
        }

        //  thread create + push
        private async Task EnsureThreadAndPushCreatedAsync(Appointment appt)
        {
            // Performance: Use Get instead of GetAll().FirstOrDefault()
            var existing = await threadDal.Get(t => t.AppointmentId == appt.Id);
            if (existing is not null) return;

            var thread = new ChatThread
            {
                Id = Guid.NewGuid(),
                AppointmentId = appt.Id,
                CustomerUserId = appt.CustomerUserId,
                StoreOwnerUserId = appt.BarberStoreUserId,
                FreeBarberUserId = appt.FreeBarberUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await threadDal.Add(thread);

            // Kat─▒l─▒mc─▒lara chat.threadCreated push
            // GetThreadsAsync mant─▒─ş─▒n─▒ kullanarak thread detaylar─▒n─▒ doldur
            await chatService.PushAppointmentThreadCreatedAsync(appt.Id);
        }

        private static string BuildThreadTitleForUser(Guid userId, Appointment appt, string? storeName)
        {
            if (appt.BarberStoreUserId == userId)
            {
                // store owner kendi listesinde kar┼ş─▒ taraf
                return appt.CustomerUserId.HasValue ? Messages.ChatThreadTitleCustomer : Messages.ChatThreadTitleFreeBarber;
            }

            // customer/freebarber taraf─▒ store'u g├Ârs├╝n
            return string.IsNullOrWhiteSpace(storeName) ? Messages.ChatThreadTitleBarberStore : storeName!;
        }

        private double MaxDistanceKm => _settings.MaxDistanceKm;

        private static double ToRad(double val) => Math.PI / 180 * val;

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static IResult EnsureValidCoords(double lat, double lon, string who)
        {
            if (lat == 0 && lon == 0)
                return new ErrorResult($"{who} konumu ayarl─▒ de─şil.");
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                return new ErrorResult($"{who} konumu ge├ğersiz.");
            return new SuccessResult();
        }

        // NOTE: This method is an overload that accepts FreeBarber entity directly
        // Used when we already have the entity loaded to avoid extra database query
        private async Task<IResult> SetFreeBarberAvailabilityAsync(FreeBarber fb, bool isAvailable)
        {
            if (fb is null) return new ErrorResult(Messages.FreeBarberNotFound);
            fb.IsAvailable = isAvailable;
            fb.UpdatedAt = DateTime.UtcNow;
            await freeBarberDal.Update(fb);
            return new SuccessResult();
        }

        private async Task<IDataResult<bool>> EnsurePendingNotExpiredAndHandleAsync(Appointment appt)
        {
            if (!appt.PendingExpiresAt.HasValue || appt.PendingExpiresAt.Value > DateTime.UtcNow)
                return new SuccessDataResult<bool>(true);

            if (appt.StoreSelectionType == StoreSelectionType.StoreSelection)
            {
                var overallExpiresAt = GetStoreSelectionOverallExpiry(appt);
                if (DateTime.UtcNow >= overallExpiresAt)
                {
                    appt.Status = AppointmentStatus.Unanswered;
                    appt.PendingExpiresAt = null;
                    appt.UpdatedAt = DateTime.UtcNow;

                    if (appt.StoreDecision == DecisionStatus.Pending)
                        appt.StoreDecision = DecisionStatus.NoAnswer;
                    if (appt.FreeBarberDecision == DecisionStatus.Pending)
                        appt.FreeBarberDecision = DecisionStatus.NoAnswer;
                    if (appt.CustomerDecision == DecisionStatus.Pending)
                        appt.CustomerDecision = DecisionStatus.NoAnswer;

                    await appointmentDal.Update(appt);

                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);
                    await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                    return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
                }

                                if (appt.BarberStoreUserId.HasValue && appt.StoreDecision == DecisionStatus.Pending)
                {
                    var storeOwnerUserId = appt.BarberStoreUserId;
                    var freeBarberUserId = appt.FreeBarberUserId;

                    appt.StoreDecision = DecisionStatus.NoAnswer;
                    appt.UpdatedAt = DateTime.UtcNow;
                    SetStoreSelectionOverallExpiry(appt);

                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

                    var recipients = new List<Guid>();
                    if (storeOwnerUserId.HasValue) recipients.Add(storeOwnerUserId.Value);
                    if (freeBarberUserId.HasValue) recipients.Add(freeBarberUserId.Value);
                    if (recipients.Count > 0)
                        await notifySvc.NotifyToRecipientsAsync(
                            appt.Id,
                            NotificationType.StoreSelectionTimeout,
                            recipients,
                            actorUserId: null);

                    ClearStoreSelectionSlot(appt);

                    await appointmentDal.Update(appt);
                    await UpdateThreadStoreOwnerAsync(appt.Id, null);
                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id,
                        appt.Status,
                        appt.StoreDecision,
                        appt.FreeBarberDecision,
                        appt.CustomerDecision,
                        appt.PendingExpiresAt
                    );

                    await NotifyAppointmentUpdateToParticipantsAsync(appt);
                    await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                    return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
                }

                                if (appt.BarberStoreUserId.HasValue &&
                    appt.StoreDecision == DecisionStatus.Approved &&
                    appt.CustomerDecision == DecisionStatus.Pending)
                {
                    var storeOwnerUserId = appt.BarberStoreUserId;
                    var freeBarberUserId = appt.FreeBarberUserId;
                    var customerUserId = appt.CustomerUserId;

                    appt.CustomerDecision = DecisionStatus.NoAnswer;
                    appt.UpdatedAt = DateTime.UtcNow;
                    appt.StoreDecision = DecisionStatus.Pending;
                    SetStoreSelectionOverallExpiry(appt);

                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

                    var recipients = new List<Guid>();
                    if (storeOwnerUserId.HasValue) recipients.Add(storeOwnerUserId.Value);
                    if (freeBarberUserId.HasValue) recipients.Add(freeBarberUserId.Value);
                    if (customerUserId.HasValue) recipients.Add(customerUserId.Value);
                    if (recipients.Count > 0)
                        await notifySvc.NotifyToRecipientsAsync(
                            appt.Id,
                            NotificationType.CustomerFinalTimeout,
                            recipients,
                            actorUserId: null);

                    ClearStoreSelectionSlot(appt);

                    await appointmentDal.Update(appt);
                    await UpdateThreadStoreOwnerAsync(appt.Id, null);
                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id,
                        appt.Status,
                        appt.StoreDecision,
                        appt.FreeBarberDecision,
                        appt.CustomerDecision,
                        appt.PendingExpiresAt
                    );

                    await NotifyAppointmentUpdateToParticipantsAsync(appt);
                    await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

                    return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
                }

                return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
            }

            appt.Status = AppointmentStatus.Unanswered;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
        }

        // Helper: Randevu durumu de─şi┼şti─şinde thread g├╝ncellemesi yap
        private async Task UpdateThreadOnAppointmentStatusChangeAsync(Appointment appt)
        {
            if (appt.Id == Guid.Empty) return;

            // Thread'i bul (hen├╝z olu┼şturulmam─▒┼ş olabilir - mesaj g├Ânderilmemi┼şse)
            var thread = await threadDal.Get(t => t.AppointmentId == appt.Id);

            // Kat─▒l─▒mc─▒lar─▒ belirle (appointment'tan al, thread'den de─şil - thread null olabilir)
            var participants = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // Durum art─▒k Pending/Approved de─şilse thread'i kald─▒r
            if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Approved)
            {
                // Thread varsa kald─▒r
                if (thread != null)
                {
                    // T├╝m kat─▒l─▒mc─▒lara thread kald─▒r─▒ld─▒─ş─▒n─▒ bildir
                    foreach (var userId in participants)
                    {
                        await realtime.PushChatThreadRemovedAsync(userId, thread.Id);
                    }
                }
                // Thread yoksa (hen├╝z olu┼şturulmam─▒┼ş) hi├ğbir ┼şey yapmaya gerek yok
                // ├ç├╝nk├╝ SendMessageAsync'te zaten status kontrol├╝ var ve Pending/Approved de─şilse mesaj g├Ânderilmez
            }
            else
            {
                // Durum hala Pending/Approved ise thread'i g├╝ncelle (status de─şi┼şmi┼ş olabilir)
                // Thread varsa g├╝ncelle
                if (thread != null)
                {
                    // PushAppointmentThreadUpdatedAsync ile thread g├╝ncellemesini g├Ânder
                    // Bu metod t├╝m kat─▒l─▒mc─▒lara thread update push eder
                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                }
                // Thread yoksa hen├╝z olu┼şturulmam─▒┼ş demektir (mesaj g├Ânderilmemi┼ş)
                // Thread olu┼şturuldu─şunda (ilk mesaj g├Ânderildi─şinde) zaten do─şru durumda olacak
            }
        }

        private async Task NotifyAppointmentUpdateToParticipantsAsync(Appointment appt)
        {
            // ─░lgili kullan─▒c─▒lar─▒ bul
            var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (participantUserIds.Count == 0) return;

            // Her kullan─▒c─▒ i├ğin g├╝ncellenmi┼ş appointment'─▒ al ve SignalR ile g├Ânder
            // Performans i├ğin: ├ûnce appointment'─▒n hangi filter'a uydu─şunu belirle
            AppointmentFilter? targetFilter = null;
            if (appt.Status == AppointmentStatus.Approved || appt.Status == AppointmentStatus.Pending)
                targetFilter = AppointmentFilter.Active;
            else if (appt.Status == AppointmentStatus.Completed)
                targetFilter = AppointmentFilter.Completed;
            else if (appt.Status == AppointmentStatus.Cancelled ||
                     appt.Status == AppointmentStatus.Rejected ||
                     appt.Status == AppointmentStatus.Unanswered)
                targetFilter = AppointmentFilter.Cancelled;

            foreach (var userId in participantUserIds)
            {
                try
                {
                    // E─şer target filter belirlenebildiyse sadece onu kontrol et
                    if (targetFilter.HasValue)
                    {
                        var appointments = await appointmentDal.GetAllAppointmentByFilter(userId, targetFilter.Value);
                        var updatedAppt = appointments.FirstOrDefault(a => a.Id == appt.Id);

                        if (updatedAppt != null)
                        {
                            await realtime.PushAppointmentUpdatedAsync(userId, updatedAppt);

                            // Badge count g├╝ncellemesi - appointment g├╝ncellemesi sonras─▒
                            var badgeSvcProperty = realtime.GetType().GetProperty("BadgeService");
                            if (badgeSvcProperty != null)
                            {
                                var badgeSvc = badgeSvcProperty.GetValue(realtime) as IBadgeService;
                                // BadgeService kullan─▒m─▒ gerekti─şinde buraya eklenecek
                            }
                            continue;
                        }
                    }

                    // E─şer target filter'da bulunamad─▒ysa veya belirlenemediyse t├╝m filter'lar─▒ kontrol et
                    var allFilters = new[] { AppointmentFilter.Active, AppointmentFilter.Completed, AppointmentFilter.Cancelled };

                    foreach (var filter in allFilters)
                    {
                        if (targetFilter.HasValue && filter == targetFilter.Value)
                            continue; // Zaten kontrol ettik

                        var appointments = await appointmentDal.GetAllAppointmentByFilter(userId, filter);
                        var updatedAppt = appointments.FirstOrDefault(a => a.Id == appt.Id);

                        if (updatedAppt != null)
                        {
                            await realtime.PushAppointmentUpdatedAsync(userId, updatedAppt);
                            break;
                        }
                    }
                }
                catch
                {
                    // Hata durumunda devam et, kritik de─şil
                }
            }
        }


    }
}


























