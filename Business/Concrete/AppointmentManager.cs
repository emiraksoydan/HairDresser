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
        
        // Timeout Süreleri:
        // - İsteğime Göre: _settings.PendingTimeoutMinutes (5 dakika)
        // - Dükkan Seç (toplam): StoreSelectionTotalMinutes (30 dakika)
        // - Dükkan onayı: StoreSelectionStepMinutes (5 dakika, toplam 30 dk'ya dahil)
        // - Müşteri onayı: Yeni süre yok, toplam 30 dakikaya dahil
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

            // Not: Store'un birden fazla active randevusu OLABİLİR demiştin.
            // Bu methodu sadece "bilgi" amaçlı tutuyorum.
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
                return new ErrorDataResult<Guid>("StoreSelectionType seçilmelidir.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.StoreSelection && string.IsNullOrWhiteSpace(req.Note))
                return new ErrorDataResult<Guid>("Randevu notu zorunludur.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.StoreSelection && (req.StoreId != Guid.Empty && req.StoreId != default))
                return new ErrorDataResult<Guid>("Dükkan Seç senaryosunda storeId gönderilemez.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.StoreSelection && req.ServiceOfferingIds != null && req.ServiceOfferingIds.Count > 0)
                return new ErrorDataResult<Guid>("Dükkan Seç senaryosunda hizmet seçilemez.");
            
            if (req.StoreSelectionType.Value == StoreSelectionType.CustomRequest && (req.ServiceOfferingIds == null || req.ServiceOfferingIds.Count == 0))
                return new ErrorDataResult<Guid>(Messages.ServiceOfferingRequired);
            
            if (req.StoreSelectionType.Value == StoreSelectionType.CustomRequest && (req.StoreId != Guid.Empty && req.StoreId != default))
                return new ErrorDataResult<Guid>("İsteğime Göre seçeneğinde dükkan seçilemez.");
            
            if (!req.RequestLatitude.HasValue || !req.RequestLongitude.HasValue)
                return new ErrorDataResult<Guid>(Messages.LocationRequired);
            
            // FreeBarber entity'sini al
            var fbEntity = await freeBarberDal.Get(x => x.FreeBarberUserId == req.FreeBarberUserId.Value);
            if (fbEntity is null) return new ErrorDataResult<Guid>(Messages.FreeBarberNotFound);
            
            // Business Rules kontrolü
            // StoreSelection senaryosunda FreeBarber meşgul olsa bile dükkana randevu isteği gönderebilir
            var businessRulesList = new List<Func<Task<IResult>>>
            {
                async () => await businessRules.CheckUserIsCustomer(customerUserId),
                async () => await businessRules.CheckFreeBarberExists(req.FreeBarberUserId.Value),
                async () => businessRules.CheckDistance(req.RequestLatitude.Value, req.RequestLongitude.Value, fbEntity.Latitude, fbEntity.Longitude, Messages.FreeBarberDistanceExceeded),
                async () => await businessRules.CheckActiveAppointmentRules(customerUserId, req.FreeBarberUserId, null, AppointmentRequester.Customer)
            };
            
            // StoreSelection senaryosunda meşgul kontrolü yapma
            if (req.StoreSelectionType.Value != StoreSelectionType.StoreSelection)
            {
                businessRulesList.Insert(2, async () => await businessRules.CheckFreeBarberAvailable(req.FreeBarberUserId.Value));
            }
            
            IResult? result = await BusinessRules.RunAsync(businessRulesList.ToArray());
            
            if (result != null)
                return new ErrorDataResult<Guid>(result.Message);
            
            // Service offering kontrolü
            if (req.StoreSelectionType.Value == StoreSelectionType.CustomRequest)
            {
                var offeringRes = await EnsureServiceOfferingsBelongToOwnerAsync(req.ServiceOfferingIds, fbEntity.Id);
                if (!offeringRes.Success) return new ErrorDataResult<Guid>(offeringRes.Message);
            }
            
            // StoreSelectionType'a göre timeout belirle
            int timeoutMinutes = req.StoreSelectionType.Value == StoreSelectionType.CustomRequest 
                ? _settings.PendingTimeoutMinutes
                : StoreSelectionTotalMinutes;
            
            // Randevu oluştur
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
                // Dükkan Seç: FreeBarber 30dk içinde red edebilir, dükkan henüz yok
                appt.AppointmentDate = null;
                appt.StartTime = null;
                appt.EndTime = null;
            }
            // İsteğime Göre senaryosunda da decision'lar null kalır
            // FreeBarber karar verdiğinde Customer'a bildirim gider
            
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
            
            // Business Rules kontrolü - Core.Utilities.Business.BusinessRules.RunAsync kullanımı
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
            
            // Business Rules kontrolü - Core.Utilities.Business.BusinessRules.RunAsync kullanımı
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

            // chair seçilmişse store’a ait + overlap kontrol
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
            // Business Rules kontrolü
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId && x.BarberStoreOwnerId == storeOwnerUserId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFoundOrNotOwner);
            
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == req.FreeBarberUserId);
            if (fb is null) return new ErrorDataResult<Guid>(Messages.FreeBarberNotFound);

            // Free barber'ın pending/approved randevusu varsa berber çağrıldı olsun (meşgul)
            // CheckFreeBarberAvailable kontrolünü atlıyoruz - pending/approved randevusu varsa yeni randevu oluşturulabilir
            var hasActiveAppointment = await appointmentDal.AnyAsync(x =>
                x.FreeBarberUserId == req.FreeBarberUserId &&
                Active.Contains(x.Status));
            
            // Eğer aktif randevu varsa free barber meşgul olmalı
            if (hasActiveAppointment && fb.IsAvailable)
            {
                var freeBarberLockRes = await SetFreeBarberAvailabilityAsync(fb, false);
                if (!freeBarberLockRes.Success) return new ErrorDataResult<Guid>(freeBarberLockRes.Message);
            }
            // Eğer aktif randevu yoksa ve free barber meşgul değilse, CheckFreeBarberAvailable kontrolü yap
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
        /// Free barber, müşteri randevusuna dükkan ekler (Dükkan Seç senaryosu)
        /// </summary>
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> AddStoreToExistingAppointmentAsync(Guid freeBarberUserId, Guid appointmentId, Guid storeId, Guid chairId, DateOnly appointmentDate, TimeSpan startTime, TimeSpan endTime, List<Guid> serviceOfferingIds)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);

            // Sadece free barber bu işlemi yapabilir
            if (appt.FreeBarberUserId != freeBarberUserId)
                return new ErrorDataResult<bool>(false, Messages.Unauthorized);

            // Sadece Customer -> FreeBarber randevusu olmalı (StoreSelectionType.StoreSelection)
            if (appt.StoreSelectionType != StoreSelectionType.StoreSelection)
                return new ErrorDataResult<bool>(false, "Bu randevuya dükkan eklenemez.");

            if (serviceOfferingIds == null || serviceOfferingIds.Count == 0)
                return new ErrorDataResult<bool>(false, Messages.ServiceOfferingRequired);

            if (appt.CustomerUserId == null || appt.BarberStoreUserId != null)
                return new ErrorDataResult<bool>(false, "Bu randevuya dükkan eklenemez.");

            // Randevu hala pending olmalı
            if (appt.Status != AppointmentStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentNotPendingStatus);

            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;

            // Business Rules kontrolü
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

            // Randevuya dükkan bilgisini ekle
            appt.BarberStoreUserId = store.BarberStoreOwnerId;
            appt.ChairId = chairId;
            // Dükkan için 5 dakikalık onay süresi (ama toplam 30 dakikaya dahil)
            SetStoreSelectionStepExpiry(appt);
            appt.AppointmentDate = appointmentDate;
            appt.StartTime = startTime;
            appt.EndTime = endTime;
            appt.StoreDecision = DecisionStatus.Pending; // Store 5dk içinde onay verecek
            // FreeBarberDecision hala Pending (30dk içinde red edebilir)
            // CustomerDecision hala null (Store onayladıktan sonra Pending olacak)
            appt.UpdatedAt = DateTime.UtcNow;

            // Manuel barber kontrolü
            appt.ManuelBarberId = chair.ManuelBarberId;

            await appointmentDal.Update(appt);
            await ReplaceAppointmentServiceOfferingsAsync(appt.Id, serviceOfferingIds);

            await UpdateThreadStoreOwnerAsync(appt.Id, appt.BarberStoreUserId);

            // Thread'i güncelle (3'lü thread olacak)
            await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);

            // Dükkana bildirim gönder (sadece dükkan, müşteriye gönderme)
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

            // İlgili kullanıcılara appointment güncellemesini bildir
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
                // StoreDecision null veya Pending olmalı
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

            // ekstra: aynı taraf tekrar karar veremesin (null veya Pending olmalı)
            if (appt.StoreDecision.HasValue && appt.StoreDecision.Value != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);

            appt.StoreDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                // Customer -> FreeBarber + Store senaryosunda reddetme
                if (appt.CustomerUserId.HasValue && appt.FreeBarberUserId.HasValue)
                {
                    // Thread'den dükkan çıkarılacak, koltuk müsait olacak
                    ClearStoreSelectionSlot(appt);
                    appt.StoreDecision = DecisionStatus.Rejected;
                    // Status hala Pending kalacak, free barber tekrar dükkan arayabilir
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
                    // Dükkan onayladı, şimdi müşteri onayı bekleniyor
                    // Status hala Pending kalacak, CustomerDecision bekleniyor
                    appt.CustomerDecision = DecisionStatus.Pending;
                    // Müşteri onayı için 30 dakikalık toplam süre devam ediyor (yeni süre eklenmez)
                    SetStoreSelectionOverallExpiry(appt);
                    
                    // AppointmentDecisionUpdated bildirimleri kaldırıldı - kullanıcı isteği
                }
                // Normal senaryo: freebarber yoksa FreeBarberDecision zaten Approved -> direkt Approved olur
                else if (appt.FreeBarberDecision == DecisionStatus.Approved)
                {
                    appt.Status = AppointmentStatus.Approved;
                    appt.ApprovedAt = DateTime.UtcNow;
                    appt.PendingExpiresAt = null;
                }
            }

            await appointmentDal.Update(appt);

            // Decision verildikten sonra notification payload'larını güncelle (status, decisions)
            // Bu sayede frontend'de butonlar doğru şekilde gizlenir
            await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                appt.Id,
                appt.Status,
                appt.StoreDecision,
                appt.FreeBarberDecision,
                appt.CustomerDecision,
                appt.PendingExpiresAt
            );

            // ÖNEMLİ: Decision başarılı ise (Approved) notification'ları read yap
            // Rejected durumunda read yapılmamalı (kullanıcı görmeli)
            if (appt.Status == AppointmentStatus.Approved)
            {
                var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                // Actor (karar veren kişi - storeOwnerUserId) hariç diğer kullanıcıların notification'larını read yap
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

                // Rejected durumunda chat mesajı gönder
                try
                {
                    var rejectionMessage = "Randevu talebiniz reddedildi.";
                    if (appt.CustomerUserId.HasValue)
                    {
                        await chatService.SendMessageAsync(storeOwnerUserId, appt.Id, rejectionMessage);
                    }
                }
                catch
                {
                    // Chat mesajı gönderilemezse devam et, kritik değil
                }

                await UpdateThreadOnAppointmentStatusChangeAsync(appt);

                // İlgili kullanıcılara appointment güncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                return new SuccessDataResult<bool>(true);
            }

            if (appt.Status == AppointmentStatus.Approved)
            {
                // Approved durumunda serbest berberi meşgul yap (eğer varsa ve zaten meşgul değilse)
                if (appt.FreeBarberUserId.HasValue)
                {
                    var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                    if (fb is not null && fb.IsAvailable)
                    {
                        await SetFreeBarberAvailabilityAsync(fb, false);
                    }
                }
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: storeOwnerUserId);

                // İlgili kullanıcılara appointment güncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                return new SuccessDataResult<bool>(true);
            }

            // AppointmentDecisionUpdated bildirimleri kaldırıldı - kullanıcı isteği

            // Decision güncellendiğinde ilgili kullanıcılara appointment güncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Decision güncellendiğinde chat mesajı gönder
            try
            {
                var decisionMessage = approve ? "Randevu talebiniz kabul edildi. Diğer tarafın onayı bekleniyor." : "Randevu talebiniz reddedildi.";
                if (appt.CustomerUserId.HasValue)
                {
                    await chatService.SendMessageAsync(storeOwnerUserId, appt.Id, decisionMessage);
                }
            }
            catch
            {
                // Chat mesajı gönderilemezse devam et, kritik değil
            }

            // Transaction commit sonrası badge update'leri çalıştır
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

            // 3'lü sistemde (StoreSelection): FreeBarber tüm randevu Approved olana kadar ve 30dk dolmadan red edebilir
            var isStoreSelectionFlow = appt.StoreSelectionType == StoreSelectionType.StoreSelection && 
                                      appt.CustomerUserId.HasValue;
            
            if (isStoreSelectionFlow)
            {
                // 30 dakikalık toplam süre kontrolü
                var now = DateTime.UtcNow;
                var overallExpiresAt = appt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                if (approve)
                    return new ErrorDataResult<bool>(false, "Bu randevuda serbest berber onay adımı yok. Dükkan seçimi bekleniyor.");

                // Müşteri onay verdiyse artık free barber reddedemez
                if (appt.CustomerDecision == DecisionStatus.Approved)
                    return new ErrorDataResult<bool>(false, "Müşteri onay verdiği için bu randevu artık reddedilemez.");
                
                // Randevu tamamı Approved olduysa red edemez
                if (appt.Status == AppointmentStatus.Approved)
                    return new ErrorDataResult<bool>(false, "Randevu onaylandı, artık red edemezsiniz.");
                
                // 30 dakika dolmadıysa red edebilir (FreeBarberDecision durumuna bakmadan)
                if (now > overallExpiresAt)
                    return new ErrorDataResult<bool>(false, "Reddetme süresi doldu.");
            }
            else
            {
                // Diğer senaryolarda: FreeBarberDecision null veya Pending olmalı
                if (appt.FreeBarberDecision.HasValue && appt.FreeBarberDecision.Value != DecisionStatus.Pending)
                    return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);
            }

            appt.FreeBarberDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                // FreeBarber reddetti
                
                // StoreSelection (Dükkan Seç) senaryosu: Müşteriden gelen ilk istek
                if (appt.StoreSelectionType == StoreSelectionType.StoreSelection && 
                    appt.CustomerUserId.HasValue)
                {
                    // 30 dakikalık süre dolmadığını kontrol et (opsiyonel güvenlik kontrolü)
                    var now = DateTime.UtcNow;
                    var overallExpiresAt = appt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                    if (now > overallExpiresAt)
                        return new ErrorDataResult<bool>(false, "Reddetme süresi doldu.");
                    
                    appt.Status = AppointmentStatus.Rejected;
                    appt.PendingExpiresAt = null;
                    
                    // Eğer dükkan seçilmişse temizle
                    if (appt.BarberStoreUserId.HasValue)
                    {
                        ClearStoreSelectionSchedule(appt);
                        await UpdateThreadStoreOwnerAsync(appt.Id, null);
                        await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    }
                    
                    await appointmentDal.Update(appt);
                    
                    // FreeBarber'ı müsait yap
                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    
                    // Thread'i pasif yap
                    await UpdateThreadOnAppointmentStatusChangeAsync(appt);
                    
                    // Notification payload güncelle
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id,
                        appt.Status,
                        appt.StoreDecision,
                        appt.FreeBarberDecision,
                        appt.CustomerDecision,
                        appt.PendingExpiresAt
                    );
                    
                    // Müşteri'ye özel bildirim: FreeBarberRejectedInitial
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.FreeBarberRejectedInitial, actorUserId: freeBarberUserId);
                    
                    // SignalR ile bildir
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);
                    await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();
                    
                    return new SuccessDataResult<bool>(true);
                }
                
                // Diğer senaryolar (CustomRequest, Store -> FreeBarber, vs.)
                appt.Status = AppointmentStatus.Rejected;
                appt.PendingExpiresAt = null;

                // Customer -> FreeBarber + Store senaryosunda FreeBarber reddederse
                if (appt.CustomerUserId.HasValue && appt.BarberStoreUserId.HasValue)
                {
                    // Dükkan thread'den çıkarılacak, koltuk müsait olacak
                    ClearStoreSelectionSchedule(appt);
                    await UpdateThreadStoreOwnerAsync(appt.Id, null);
                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    
                    // 3'lü sistemde FreeBarber dükkandan sonra reddetti
                    await appointmentDal.Update(appt);
                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    await UpdateThreadOnAppointmentStatusChangeAsync(appt);
                    
                    // Notification payload güncelle
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id,
                        appt.Status,
                        appt.StoreDecision,
                        appt.FreeBarberDecision,
                        appt.CustomerDecision,
                        appt.PendingExpiresAt
                    );
                    
                    // Müşteri ve Store'a bildir
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.FreeBarberRejectedInitial, actorUserId: freeBarberUserId);
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);
                    await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();
                    
                    return new SuccessDataResult<bool>(true);
                }
            }
            else
            {
                // FreeBarber onayladı
                
                // Customer -> FreeBarber randevusu
                if (appt.CustomerUserId.HasValue && appt.BarberStoreUserId == null)
                {
                    // İsteğime Göre (CustomRequest) senaryosu: FreeBarber onayladı, şimdi Customer onayı bekleniyor
                    if (appt.StoreSelectionType == StoreSelectionType.CustomRequest)
                    {
                        // Status hala Pending, CustomerDecision bekleniyor
                        appt.CustomerDecision = DecisionStatus.Pending;
                        // FreeBarberDecision zaten Approved olarak set edildi (satır 798)
                    }
                    // Dükkan Seç senaryosunda: FreeBarber onayladıktan sonra dükkan arayacak
                    // Bu durumda FreeBarberDecision Pending kalır (randevu sonuna kadar)
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
            }

            await appointmentDal.Update(appt);

            // Decision verildikten sonra notification payload'larını güncelle (status, decisions)
            // Bu sayede frontend'de butonlar doğru şekilde gizlenir
            await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                appt.Id,
                appt.Status,
                appt.StoreDecision,
                appt.FreeBarberDecision,
                appt.CustomerDecision,
                appt.PendingExpiresAt
            );

            // ÖNEMLİ: Decision başarılı ise (Approved) notification'ları read yap
            // Rejected durumunda read yapılmamalı (kullanıcı görmeli)
            if (appt.Status == AppointmentStatus.Approved)
            {
                var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                // Actor (karar veren kişi - freeBarberUserId) hariç diğer kullanıcıların notification'larını read yap
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

                // Rejected durumunda chat mesajı gönder
                try
                {
                    var rejectionMessage = "Randevu talebiniz reddedildi.";
                    if (appt.CustomerUserId.HasValue)
                    {
                        await chatService.SendMessageAsync(freeBarberUserId, appt.Id, rejectionMessage);
                    }
                    else if (appt.BarberStoreUserId.HasValue)
                    {
                        await chatService.SendMessageAsync(freeBarberUserId, appt.Id, rejectionMessage);
                    }
                }
                catch
                {
                    // Chat mesajı gönderilemezse devam et, kritik değil
                }

                await UpdateThreadOnAppointmentStatusChangeAsync(appt);

                // İlgili kullanıcılara appointment güncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                return new SuccessDataResult<bool>(true);
            }

            if (appt.Status == AppointmentStatus.Approved)
            {
                // Approved durumunda serbest berberi meşgul yap (eğer zaten meşgul değilse)
                var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
                if (fb is not null && fb.IsAvailable)
                {
                    await SetFreeBarberAvailabilityAsync(fb, false);
                }
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: freeBarberUserId);

                // Decision sonrası chat mesajı gönder
                try
                {
                    var decisionMessage = approve ? "Randevu talebiniz kabul edildi." : "Randevu talebiniz reddedildi.";
                    if (appt.CustomerUserId.HasValue)
                    {
                        await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                    }
                    else if (appt.BarberStoreUserId.HasValue)
                    {
                        await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                    }
                }
                catch
                {
                    // Chat mesajı gönderilemezse devam et, kritik değil
                }

                // İlgili kullanıcılara appointment güncellemesini bildir (aktif tab'da görünmesi için)
                await NotifyAppointmentUpdateToParticipantsAsync(appt);
                
                return new SuccessDataResult<bool>(true);
            }

            // AppointmentDecisionUpdated bildirimleri kaldırıldı - kullanıcı isteği

            // Decision güncellendiğinde chat mesajı gönder
            try
            {
                var decisionMessage = approve ? "Randevu talebiniz kabul edildi. Diğer tarafın onayı bekleniyor." : "Randevu talebiniz reddedildi.";
                if (appt.CustomerUserId.HasValue)
                {
                    await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                }
                else if (appt.BarberStoreUserId.HasValue)
                {
                    await chatService.SendMessageAsync(freeBarberUserId, appt.Id, decisionMessage);
                }
            }
            catch
            {
                // Chat mesajı gönderilemezse devam et, kritik değil
            }

            // Decision güncellendiğinde ilgili kullanıcılara appointment güncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri çalıştır
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- CUSTOMER DECISION (NEW) ----------------

        /// <summary>
        /// Müşteri kararı - Customer -> FreeBarber + Store senaryosunda müşteri onayı
        /// </summary>
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> CustomerDecisionAsync(Guid customerUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);
            if (appt.CustomerUserId != customerUserId) return new ErrorDataResult<bool>(false, Messages.Unauthorized);
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>(false, Messages.AppointmentNotPendingStatus);

            // İki senaryo var:
            // 1. Customer -> FreeBarber (İsteğime Göre - CustomRequest): Store yok, FreeBarber onaylamış olmalı
            // 2. Customer -> FreeBarber + Store (Dükkan Seç - StoreSelection): Store ve FreeBarber var, Store onaylamış olmalı

            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;

            // CustomerDecision null veya Pending olmalı
            if (appt.CustomerDecision.HasValue && appt.CustomerDecision.Value != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);

            // CustomRequest (İsteğime Göre) senaryosu
            if (appt.StoreSelectionType == StoreSelectionType.CustomRequest && 
                appt.FreeBarberUserId.HasValue && 
                !appt.BarberStoreUserId.HasValue)
            {
                // FreeBarber onaylamış olmalı
                if (appt.FreeBarberDecision != DecisionStatus.Approved)
                    return new ErrorDataResult<bool>(false, "Serbest berber onayı bekleniyor.");

                appt.CustomerDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
                appt.UpdatedAt = DateTime.UtcNow;

                if (!approve)
                {
                    // Müşteri reddetti
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
                    // Müşteri onayladı - randevu Approved
                    appt.Status = AppointmentStatus.Approved;
                    appt.ApprovedAt = DateTime.UtcNow;
                    appt.PendingExpiresAt = null;
                    
                    await appointmentDal.Update(appt);
                    
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id, appt.Status, appt.StoreDecision, appt.FreeBarberDecision, 
                        appt.CustomerDecision, appt.PendingExpiresAt);
                    
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: customerUserId);
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);
                    await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();
                    
                    return new SuccessDataResult<bool>(true);
                }
            }

            // StoreSelection (Dükkan Seç) senaryosu - 3'lü sistem
            if (!appt.FreeBarberUserId.HasValue || !appt.BarberStoreUserId.HasValue)
                return new ErrorDataResult<bool>(false, "Bu randevu için müşteri kararı verilemez.");

            // Store onaylamış olmalı
            if (appt.StoreDecision != DecisionStatus.Approved)
                return new ErrorDataResult<bool>(false, "Dükkan onayı bekleniyor.");

            appt.CustomerDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                await notifySvc.NotifyAsync(appt.Id, NotificationType.CustomerRejectedFinal, actorUserId: customerUserId);

                ClearStoreSelectionSlot(appt);
                SetStoreSelectionOverallExpiry(appt);
                // Müşteri reddetti - dükkan thread'den çıkarılacak, koltuk müsait olacak
                appt.StoreDecision = DecisionStatus.Pending; // Dükkan tekrar seçilebilir
                appt.CustomerDecision = null; // CustomerDecision null'a çekilir
                // Status hala Pending kalacak, free barber tekrar dükkan arayabilir
            }
            else
            {
                // Müşteri onayladı - randevu Approved olur
                appt.Status = AppointmentStatus.Approved;
                appt.ApprovedAt = DateTime.UtcNow;
                appt.PendingExpiresAt = null;
                
                // FreeBarberDecision artık Approved olur (randevu onaylandığında)
                appt.FreeBarberDecision = DecisionStatus.Approved;

                // FreeBarber ve Store'a bildirim
                await notifySvc.NotifyAsync(appt.Id, NotificationType.CustomerApprovedFinal, actorUserId: customerUserId);
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

            // İlgili kullanıcılara appointment güncellemesini bildir
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

            // İptal kuralları:
            // 1. Müşteri: Sadece Approved durumunda iptal edebilir (Pending'de edemez - çünkü talebi o gönderdi)
            // 2. FreeBarber: Hem Pending hem Approved durumunda iptal edebilir
            // 3. Store: Hem Pending hem Approved durumunda iptal edebilir
            
            if (appt.CustomerUserId == userId && appt.Status == AppointmentStatus.Approved)
            {
                return new ErrorDataResult<bool>(false, "Onaylanan randevuyu müşteri iptal edemez.");
            }

            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledByUserId = userId;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            // FreeBarber müsaitliğini serbest bırak
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            // Koltuk müsaitliğini serbest bırak (eğer varsa)
            // Not: Koltuk otomatik olarak müsait olacak çünkü status Cancelled oldu
            // GetAvailability sorgusu sadece Pending ve Approved randevuları kontrol ediyor

            // İptal edildiğinde iptal eden kişi hariç diğer tüm taraflara bildirim gönder
            // notifySvc.NotifyAsync zaten actorUserId hariç tüm katılımcılara bildirim gönderiyor
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCancelled, actorUserId: userId);

            // İptal durumunda chat mesajı gönder
            try
            {
                // İptal eden kişinin adını belirle
                string cancellerName = "Bir kullanıcı";
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
                await chatService.SendMessageAsync(userId, appt.Id, cancelMessage);
            }
            catch
            {
                // Chat mesajı gönderilemezse devam et, kritik değil
            }

            // Thread güncellemesi (thread kaldırılacak)
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);

            // İlgili kullanıcılara appointment güncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri çalıştır
            await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();

            return new SuccessDataResult<bool>(true);
        }
        [TransactionScopeAspect]

        public async Task<IDataResult<bool>> CompleteAsync(Guid userId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(Messages.AppointmentNotFound);

            // Customer -> FreeBarber (İsteğime Göre) senaryosunda free barber tamamlayabilir
            bool canComplete = false;
            if (appt.CustomerUserId.HasValue && appt.FreeBarberUserId.HasValue && appt.BarberStoreUserId == null)
            {
                // İsteğime Göre senaryosu - free barber tamamlayabilir
                canComplete = appt.FreeBarberUserId == userId;
            }
            else if (appt.BarberStoreUserId.HasValue)
            {
                // Normal senaryo - sadece store owner tamamlayabilir
                canComplete = appt.BarberStoreUserId == userId;
            }

            if (!canComplete) return new ErrorDataResult<bool>(Messages.Unauthorized);

            if (appt.Status != AppointmentStatus.Approved) return new ErrorDataResult<bool>(Messages.AppointmentNotApproved);

            // İsteğe Göre randevularda (CustomRequest ve store yok) tarih/saat kontrolü yapma
            // Bu randevularda AppointmentDate ve StartTime/EndTime null olabilir
            var isCustomRequestWithoutStore = appt.StoreSelectionType.HasValue &&
                appt.StoreSelectionType.Value == StoreSelectionType.CustomRequest &&
                appt.CustomerUserId.HasValue &&
                appt.FreeBarberUserId.HasValue &&
                !appt.BarberStoreUserId.HasValue;

            // Normal randevularda (dükkan dahil) tarih/saat kontrolü yap
            if (!isCustomRequestWithoutStore)
            {
                // TR saati ile randevu başlangıç ve bitiş tarihlerini kontrol et
                var startTrRes = GetAppointmentStartTr(appt);
                if (!startTrRes.Success) return new ErrorDataResult<bool>(startTrRes.Message);

                var endTrRes = GetAppointmentEndTr(appt);
                if (!endTrRes.Success) return new ErrorDataResult<bool>(endTrRes.Message);

                var nowTr = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

                // Randevu başlangıç tarihi geçmiş olmalı (randevu başlamış olmalı)
                if (nowTr < startTrRes.Data)
                    return new ErrorDataResult<bool>(Messages.AppointmentTimeNotPassed);

                // Randevu bitiş tarihi geçmiş olmalı (randevu bitmiş olmalı)
                if (nowTr < endTrRes.Data)
                    return new ErrorDataResult<bool>(Messages.AppointmentTimeNotPassed);
            }

            appt.Status = AppointmentStatus.Completed;
            appt.CompletedAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            // FreeBarber müsaitliğini serbest bırak
            // Completed durumunda serbest berberi müsait yap
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCompleted, actorUserId: userId);

            // Tamamlanma durumunda chat mesajı gönder
            try
            {
                var completeMessage = "Randevu tamamlandı.";
                await chatService.SendMessageAsync(userId, appt.Id, completeMessage);
            }
            catch
            {
                // Chat mesajı gönderilemezse devam et, kritik değil
            }

            // Thread güncellemesi (thread kaldırılacak)
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);

            // İlgili kullanıcılara appointment güncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri çalıştır
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

            // AddRange ile toplu ekleme - performans için daha iyi
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
            // ÖNEMLİ: Unique index tüm status'leri kontrol ediyor (ChairId, AppointmentDate, StartTime, EndTime)
            // Bu yüzden aynı slot'ta herhangi bir status'te randevu varsa (Pending, Approved, Cancelled, Rejected, Completed, Unanswered)
            // yeni randevu oluşturulamaz
            // Ancak mantıken sadece Pending ve Approved randevular slot'u dolu tutmalı
            // Diğer status'ler (Cancelled, Rejected, Completed, Unanswered) slot'u boşaltmalı

            // Önce mantıksal overlap kontrolü: Sadece Pending ve Approved randevular slot'u dolu tutar
            var hasActiveOverlap = await appointmentDal.AnyAsync(x =>
                x.ChairId == chairId &&
                x.AppointmentDate == date &&
                (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved) &&
                x.StartTime < end &&
                x.EndTime > start);

            if (hasActiveOverlap)
                return new ErrorResult(Messages.AppointmentSlotOverlap);

            // NOTE: Unique index (ChairId, AppointmentDate, StartTime, EndTime) zaten var
            // Bu index aynı slot'ta herhangi bir randevu oluşturulmasını engeller
            // Exact match kontrolü gereksiz çünkü unique constraint zaten bunu yapıyor
            // Eğer exact match varsa, Add() çağrısında DbUpdateException fırlatılacak
            // ve catch bloğunda yakalanacak (satır 177, 298, 402)

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
                return new ErrorResult("Dükkan şu an kapalı. Lütfen mesai saatleri içinde randevu oluşturun.");

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

                // local time (TR) olarak DateTime döndürüyoruz
                // (DateTime.Now ile kıyas için)
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

                // local time (TR) olarak DateTime döndürüyoruz
                // (DateTime.Now ile kıyas için)
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
        /// Ortak appointment oluşturma işlemleri (service offerings, thread, notification, badge update)
        /// </summary>
        private async Task FinalizeAppointmentCreationAsync(Appointment appt, List<Guid>? serviceOfferingIds, Guid actorUserId)
        {
            // Service offerings snapshot
            await CreateAppointmentServiceOfferingsAsync(appt.Id, serviceOfferingIds);

            // Thread oluştur ve push et
            await EnsureThreadAndPushCreatedAsync(appt);

            // Notification gönder
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

            // Katılımcılara chat.threadCreated push
            // GetThreadsAsync mantığını kullanarak thread detaylarını doldur
            await chatService.PushAppointmentThreadCreatedAsync(appt.Id);
        }

        private static string BuildThreadTitleForUser(Guid userId, Appointment appt, string? storeName)
        {
            if (appt.BarberStoreUserId == userId)
            {
                // store owner kendi listesinde karşı taraf
                return appt.CustomerUserId.HasValue ? Messages.ChatThreadTitleCustomer : Messages.ChatThreadTitleFreeBarber;
            }

            // customer/freebarber tarafı store'u görsün
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
                return new ErrorResult($"{who} konumu ayarlı değil.");
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                return new ErrorResult($"{who} konumu geçersiz.");
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
                    appt.StoreDecision = DecisionStatus.NoAnswer;
                    appt.UpdatedAt = DateTime.UtcNow;

                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);

                    ClearStoreSelectionSlot(appt);
                    SetStoreSelectionOverallExpiry(appt);

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
                    appt.CustomerDecision = DecisionStatus.NoAnswer;
                    appt.UpdatedAt = DateTime.UtcNow;

                    await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);

                    ClearStoreSelectionSlot(appt);
                    appt.StoreDecision = DecisionStatus.Pending;
                    SetStoreSelectionOverallExpiry(appt);

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

        // Helper: Randevu durumu değiştiğinde thread güncellemesi yap
        private async Task UpdateThreadOnAppointmentStatusChangeAsync(Appointment appt)
        {
            if (appt.Id == Guid.Empty) return;

            // Thread'i bul (henüz oluşturulmamış olabilir - mesaj gönderilmemişse)
            var thread = await threadDal.Get(t => t.AppointmentId == appt.Id);

            // Katılımcıları belirle (appointment'tan al, thread'den değil - thread null olabilir)
            var participants = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // Durum artık Pending/Approved değilse thread'i kaldır
            if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Approved)
            {
                // Thread varsa kaldır
                if (thread != null)
                {
                    // Tüm katılımcılara thread kaldırıldığını bildir
                    foreach (var userId in participants)
                    {
                        await realtime.PushChatThreadRemovedAsync(userId, thread.Id);
                    }
                }
                // Thread yoksa (henüz oluşturulmamış) hiçbir şey yapmaya gerek yok
                // Çünkü SendMessageAsync'te zaten status kontrolü var ve Pending/Approved değilse mesaj gönderilmez
            }
            else
            {
                // Durum hala Pending/Approved ise thread'i güncelle (status değişmiş olabilir)
                // Thread varsa güncelle
                if (thread != null)
                {
                    // PushAppointmentThreadUpdatedAsync ile thread güncellemesini gönder
                    // Bu metod tüm katılımcılara thread update push eder
                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                }
                // Thread yoksa henüz oluşturulmamış demektir (mesaj gönderilmemiş)
                // Thread oluşturulduğunda (ilk mesaj gönderildiğinde) zaten doğru durumda olacak
            }
        }

        private async Task NotifyAppointmentUpdateToParticipantsAsync(Appointment appt)
        {
            // İlgili kullanıcıları bul
            var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (participantUserIds.Count == 0) return;

            // Her kullanıcı için güncellenmiş appointment'ı al ve SignalR ile gönder
            // Performans için: Önce appointment'ın hangi filter'a uyduğunu belirle
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
                    // Eğer target filter belirlenebildiyse sadece onu kontrol et
                    if (targetFilter.HasValue)
                    {
                        var appointments = await appointmentDal.GetAllAppointmentByFilter(userId, targetFilter.Value);
                        var updatedAppt = appointments.FirstOrDefault(a => a.Id == appt.Id);

                        if (updatedAppt != null)
                        {
                            await realtime.PushAppointmentUpdatedAsync(userId, updatedAppt);

                            // Badge count güncellemesi - appointment güncellemesi sonrası
                            var badgeSvcProperty = realtime.GetType().GetProperty("BadgeService");
                            if (badgeSvcProperty != null)
                            {
                                var badgeSvc = badgeSvcProperty.GetValue(realtime) as IBadgeService;
                                // BadgeService kullanımı gerektiğinde buraya eklenecek
                            }
                            continue;
                        }
                    }

                    // Eğer target filter'da bulunamadıysa veya belirlenemediyse tüm filter'ları kontrol et
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
                    // Hata durumunda devam et, kritik değil
                }
            }
        }


    }
}


























