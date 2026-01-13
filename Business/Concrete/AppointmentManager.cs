using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Abstract;
using Business.Helpers;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Business;
using Core.Utilities.Configuration;
using Core.Utilities.Helpers;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

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
        IUserDal userDal,
        AppointmentBusinessRules businessRules
    ) : IAppointmentService
    {
        private static readonly AppointmentStatus[] Active = [AppointmentStatus.Pending, AppointmentStatus.Approved];
        private readonly AppointmentSettings _settings = appointmentSettings.Value;

        private const int StoreSelectionTotalMinutes = 30;
        private const int StoreSelectionStepMinutes = 5;

        // NOT: ProcessBadgeUpdatesAfterCommit() kaldırıldı
        // TransactionScopeAspect artık transaction commit sonrası otomatik olarak badge update'leri çalıştırıyor

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


        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<AppointmentGetDto>>> GetAllAppointmentByFilter(Guid currentUserId, AppointmentFilter appointmentFilter)
        {
            var result = await appointmentDal.GetAllAppointmentByFilter(currentUserId, appointmentFilter);
            return new SuccessDataResult<List<AppointmentGetDto>>(result);
        }

        // ---------------- CREATE: CUSTOMER -> FREEBARBER (NEW) ----------------

        [SecuredOperation("Customer")]
        [LogAspect]
        [ValidationAspect(typeof(CreateCustomerToFreeBarberRequestDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateCustomerToFreeBarberAsync(Guid customerUserId, CreateAppointmentRequestDto req)
        {

            // FreeBarber entity'sini al
            var fbEntity = await freeBarberDal.Get(x => x.FreeBarberUserId == req.FreeBarberUserId.Value);
            if (fbEntity is null) return new ErrorDataResult<Guid>(Messages.FreeBarberNotFound);

            // Business Rules kontrol├╝
            // StoreSelection senaryosunda FreeBarber me┼şgul olsa bile d├╝kkana randevu iste─şi g├Ânderebilir
            var businessRulesList = new List<Func<Task<IResult>>>
            {
                async () => await businessRules.CheckUserIsCustomer(customerUserId),
                async () => await businessRules.CheckFreeBarberExists(req.FreeBarberUserId.Value),
                () => Task.FromResult(businessRules.CheckDistance(req.RequestLatitude.Value, req.RequestLongitude.Value, fbEntity.Latitude, fbEntity.Longitude, Messages.FreeBarberDistanceExceeded)),
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
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            var lockRes = await SetFreeBarberAvailabilityAsync(fbEntity, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await FinalizeAppointmentCreationAsync(appt, req.ServiceOfferingIds, customerUserId);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- CREATE: CUSTOMER -> STORE ----------------

        [SecuredOperation("Customer")]
        [LogAspect]
        [ValidationAspect(typeof(CreateCustomerToStoreRequestDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateCustomerToStoreControlAsync(Guid customerUserId, CreateAppointmentRequestDto req)
        {

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
                () => Task.FromResult(businessRules.CheckTimeRangeValid(start, end)),
                () => Task.FromResult(businessRules.CheckDateNotPast(appointmentDate, start)),
                () => Task.FromResult(businessRules.CheckDistance(req.RequestLatitude.Value, req.RequestLongitude.Value, store.Latitude, store.Longitude, Messages.CustomerDistanceExceeded)),
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
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            await FinalizeAppointmentCreationAsync(appt, req.ServiceOfferingIds, customerUserId);
            
            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor
            
            return new SuccessDataResult<Guid>(appt.Id);
        }
        // ---------------- CREATE: FREEBARBER -> STORE ----------------

        [SecuredOperation("FreeBarber")]
        [LogAspect]
        [ValidationAspect(typeof(CreateFreeBarberToStoreRequestDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req)
        {

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
                () => Task.FromResult(businessRules.CheckTimeRangeValid(start, end)),
                () => Task.FromResult(businessRules.CheckDateNotPast(appointmentDate, start)),
                () => Task.FromResult(businessRules.CheckDistance(fb.Latitude, fb.Longitude, store.Latitude, store.Longitude, Messages.FreeBarberStoreDistanceExceeded)),
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
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await FinalizeAppointmentCreationAsync(appt, req.ServiceOfferingIds, freeBarberUserId);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<Guid>(appt.Id);
        }


        [SecuredOperation("BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(CreateStoreToFreeBarberRequestDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateStoreToFreeBarberRequestDto req)
        {
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId && x.BarberStoreOwnerId == storeOwnerUserId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFoundOrNotOwner);

            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == req.FreeBarberUserId);
            if (fb is null) return new ErrorDataResult<Guid>(Messages.FreeBarberNotFound);

            var hasActiveAppointment = await appointmentDal.AnyAsync(x =>
                x.FreeBarberUserId == req.FreeBarberUserId &&
                Active.Contains(x.Status));

            if (hasActiveAppointment && fb.IsAvailable)
            {
                var freeBarberLockRes = await SetFreeBarberAvailabilityAsync(fb, false);
                if (!freeBarberLockRes.Success) return new ErrorDataResult<Guid>(freeBarberLockRes.Message);
            }
            else if (!hasActiveAppointment)
            {
                var availableResult = await businessRules.CheckFreeBarberAvailable(req.FreeBarberUserId);
                if (!availableResult.Success) return new ErrorDataResult<Guid>(availableResult.Message);
            }

            IResult? result = await BusinessRules.RunAsync(
                async () => await businessRules.CheckStoreOwnership(req.StoreId, storeOwnerUserId),
                async () => await businessRules.CheckFreeBarberExists(req.FreeBarberUserId),
                () => Task.FromResult(businessRules.CheckDistance(store.Latitude, store.Longitude, fb.Latitude, fb.Longitude, Messages.StoreFreeBarberDistanceExceeded)),
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
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await FinalizeAppointmentCreationAsync(appt, serviceOfferingIds: null, storeOwnerUserId);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- ADD STORE TO EXISTING CUSTOMER->FREEBARBER APPOINTMENT ----------------

        /// <summary>
        /// Free barber, müşteri randevusuna dükkan ekler (Dükkan Seç senaryosu)
        /// </summary>
        [SecuredOperation("FreeBarber")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> AddStoreToExistingAppointmentAsync(Guid freeBarberUserId, Guid appointmentId, Guid storeId, Guid chairId, DateOnly appointmentDate, TimeSpan startTime, TimeSpan endTime, List<Guid> serviceOfferingIds)
        {
            // DTO validation (serviceOfferingIds kontrolü)
            if (serviceOfferingIds == null || serviceOfferingIds.Count == 0)
                return new ErrorDataResult<bool>(false, Messages.ServiceOfferingRequired);

            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);

            // Sadece free barber bu işlemi yapabilir
            if (appt.FreeBarberUserId != freeBarberUserId)
                return new ErrorDataResult<bool>(false, Messages.Unauthorized);

            // Sadece Customer -> FreeBarber randevusu olmalı (StoreSelectionType.StoreSelection)
            if (appt.StoreSelectionType != StoreSelectionType.StoreSelection)
                return new ErrorDataResult<bool>(false, Messages.AppointmentCannotAddStore);

            if (appt.CustomerUserId == null || appt.BarberStoreUserId != null)
                return new ErrorDataResult<bool>(false, Messages.AppointmentCannotAddStore);

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
                () => Task.FromResult(businessRules.CheckTimeRangeValid(startTime, endTime)),
                () => Task.FromResult(businessRules.CheckDateNotPast(appointmentDate, startTime)),
                async () => await businessRules.CheckStoreExists(storeId),
                async () => await businessRules.CheckChairBelongsToStore(chairId, storeId),
                async () => await businessRules.CheckFreeBarberExists(freeBarberUserId),
                () => Task.FromResult(businessRules.CheckDistance(fb.Latitude, fb.Longitude, store.Latitude, store.Longitude, Messages.FreeBarberStoreDistanceExceeded)),
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

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- DECISIONS (STORE / FREEBARBER) ----------------
        [SecuredOperation("BarberStore")]
        [LogAspect]
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
                // StoreSelection akışında PendingExpiresAt her adımda değişir (store 5dk -> customer 5dk -> overall 30dk)
                // Store'un kendi "AppointmentCreated" bildirimi, store'a gönderildiği andaki PendingExpiresAt ile kayıtlıdır.
                // Bu yüzden payload güncellemesini önce eski PendingExpiresAt ile yapıp store bildiriminin butonlarını kapatıyoruz.
                var previousPendingExpiresAt = appt.PendingExpiresAt;

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
                    // Customer 30 dakikalık genel süreye dahil, 5 dakika kuralı yok
                    SetStoreSelectionOverallExpiry(appt);
                }

                await appointmentDal.Update(appt);

                if (!approve)
                {
                    await UpdateThreadStoreOwnerAsync(appt.Id, null);
                }

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                // 1) Önce store karar adımına ait eski PendingExpiresAt ile (store'un kendi action notification'ı güncellensin)
                if (previousPendingExpiresAt.HasValue)
                {
                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                        appt.Id,
                        appt.Status,
                        appt.StoreDecision,
                        appt.FreeBarberDecision,
                        appt.CustomerDecision,
                        previousPendingExpiresAt
                    );
                }

                // 2) Sonra mevcut PendingExpiresAt ile (varsa diğer notification'lar da senkronize olsun)
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

                    // Rejected: Actor'ın (store) bildirimlerini otomatik okunmuş yap
                    await notificationService.MarkReadByAppointmentIdAsync(storeOwnerUserId, appt.Id);
                }
                else
                {
                    await notifySvc.NotifyAsync(appt.Id, NotificationType.StoreApprovedSelection, actorUserId: storeOwnerUserId);

                    // Approved: Actor'ın (store) bildirimlerini otomatik okunmuş yap
                    await notificationService.MarkReadByAppointmentIdAsync(storeOwnerUserId, appt.Id);
                }

                await NotifyAppointmentUpdateToParticipantsAsync(appt);


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


            if (appt.Status == AppointmentStatus.Rejected)
            {
                await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: storeOwnerUserId);

                // Rejected sonrası slot kilidini kaldır (availability + unique index için)
                // ÖNEMLİ: Store bilgisini (BarberStoreUserId) silme.
                // Bildirim payload'ı dolu gelsin diye notify'dan SONRA temizliyoruz.
                if (appt.ChairId.HasValue)
                {
                    appt.ChairId = null;
                    appt.ManuelBarberId = null;
                    await appointmentDal.Update(appt);
                }

                // Rejected: Actor'ın (store) bildirimlerini otomatik okunmuş yap
                await notificationService.MarkReadByAppointmentIdAsync(storeOwnerUserId, appt.Id);

                await UpdateThreadOnAppointmentStatusChangeAsync(appt);
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

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

                // Approved: Actor'ın (store) bildirimlerini otomatik okunmuş yap
                await notificationService.MarkReadByAppointmentIdAsync(storeOwnerUserId, appt.Id);

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);

                // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

                return new SuccessDataResult<bool>(true);
            }

            // AppointmentDecisionUpdated bildirimleri kald─▒r─▒ld─▒ - kullan─▒c─▒ iste─şi

            // Decision g├╝ncellendi─şinde ilgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor


            // Store kararını verdi, ilgili bildirimleri okundu olarak işaretle
            await notificationService.MarkReadByAppointmentIdAsync(storeOwnerUserId, appt.Id);

            return new SuccessDataResult<bool>(true);
        }
        [SecuredOperation("FreeBarber")]
        [LogAspect]
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
                    return new ErrorDataResult<bool>(false, Messages.FreeBarberApprovalStepNotAvailable);

                // Müşteri onay verdiyse artık free barber reddedemez
                if (appt.CustomerDecision == DecisionStatus.Approved)
                    return new ErrorDataResult<bool>(false, Messages.CannotRejectAfterCustomerApproval);

                // Randevu tamamı Approved olduysa red edemez
                if (appt.Status == AppointmentStatus.Approved)
                    return new ErrorDataResult<bool>(false, Messages.CannotRejectAfterApproval);

                // Randevu iptal olduysa red edemez
                if (appt.Status == AppointmentStatus.Cancelled)
                    return new ErrorDataResult<bool>(false, Messages.CannotRejectAfterCancellation);

                // Randevu tamamlandıysa red edemez
                if (appt.Status == AppointmentStatus.Completed)
                    return new ErrorDataResult<bool>(false, Messages.CannotRejectAfterCompletion);

                // 30 dakika dolmadıysa red edebilir (FreeBarberDecision durumuna bakmadan)
                if (now > overallExpiresAt)
                    return new ErrorDataResult<bool>(false, Messages.RejectionTimeoutExpired);
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
                    // 30 dakikalık süre dolmadığını kontrol et (opsiyonel güvenlik kontrolü)
                    var now = DateTime.UtcNow;
                    var overallExpiresAt = appt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                    if (now > overallExpiresAt)
                        return new ErrorDataResult<bool>(false, Messages.RejectionTimeoutExpired);

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

                    // Rejected: Actor'ın (freeBarber) bildirimlerini otomatik okunmuş yap
                    await notificationService.MarkReadByAppointmentIdAsync(freeBarberUserId, appt.Id);

                    // SignalR ile bildir
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                    // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

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

                    // Rejected: Actor'ın (freeBarber) bildirimlerini otomatik okunmuş yap
                    await notificationService.MarkReadByAppointmentIdAsync(freeBarberUserId, appt.Id);

                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                    // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

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

 

            if (appt.Status == AppointmentStatus.Rejected)
            {
                await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: freeBarberUserId);

                // Rejected: Actor'ın (freeBarber) bildirimlerini otomatik okunmuş yap
                await notificationService.MarkReadByAppointmentIdAsync(freeBarberUserId, appt.Id);

                await UpdateThreadOnAppointmentStatusChangeAsync(appt);

                // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

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

                // Approved: Actor'ın (freeBarber) bildirimlerini otomatik okunmuş yap
                await notificationService.MarkReadByAppointmentIdAsync(freeBarberUserId, appt.Id);

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);


                // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir (aktif tab'da g├Âr├╝nmesi i├ğin)
                await NotifyAppointmentUpdateToParticipantsAsync(appt);

                // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

    
            // Free Barber kararını verdi, ilgili bildirimleri okundu olarak işaretle
            await notificationService.MarkReadByAppointmentIdAsync(freeBarberUserId, appt.Id);

            return new SuccessDataResult<bool>(true);
            }


            // Decision g├╝ncellendi─şinde ilgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- CUSTOMER DECISION (NEW) ----------------

        /// <summary>
        /// M├╝┼şteri karar─▒ - Customer -> FreeBarber + Store senaryosunda m├╝┼şteri onay─▒
        /// </summary>
        [SecuredOperation("Customer")]
        [LogAspect]
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
                // FreeBarber onaylamış olmalı
                if (appt.FreeBarberDecision != DecisionStatus.Approved)
                    return new ErrorDataResult<bool>(false, Messages.FreeBarberApprovalPending);

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

                    await notificationService.UpdateNotificationPayloadByAppointmentAsync(appt.Id, appt.Status, appt.StoreDecision, appt.FreeBarberDecision, appt.CustomerDecision, appt.PendingExpiresAt);

                    await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: customerUserId);

                    // Rejected: Sadece actor'ın (müşteri) bildirimini read yap
                    await notificationService.MarkReadByAppointmentIdAsync(customerUserId, appt.Id);

                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                    // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

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

                    // Approved: Actor'ın (customer) bildirimlerini otomatik okunmuş yap
                    await notificationService.MarkReadByAppointmentIdAsync(customerUserId, appt.Id);

                    await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
                    await NotifyAppointmentUpdateToParticipantsAsync(appt);

                    // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

                    return new SuccessDataResult<bool>(true);
                }
            }

            // StoreSelection (Dükkan Seç) senaryosu - 3'lü sistem
            if (!appt.FreeBarberUserId.HasValue || !appt.BarberStoreUserId.HasValue)
                return new ErrorDataResult<bool>(false, Messages.CustomerDecisionNotAllowed);

            // Store onaylamış olmalı
            if (appt.StoreDecision != DecisionStatus.Approved)
                return new ErrorDataResult<bool>(false, Messages.StoreApprovalPending);

            // Customer karar adımında (StoreApprovedSelection notification) PendingExpiresAt daha sonra değişebilir.
            // Bu yüzden mevcut değeri saklıyoruz; reject senaryosunda customer'ın action notification'ını doğru güncellemek için kullanacağız.
            var previousPendingExpiresAt = appt.PendingExpiresAt;

            appt.CustomerDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                await notifySvc.NotifyAsync(appt.Id, NotificationType.CustomerRejectedFinal, actorUserId: customerUserId);

                // CustomerRejectedFinal: Actor'ın (customer) bildirimlerini otomatik okunmuş yap
                await notificationService.MarkReadByAppointmentIdAsync(customerUserId, appt.Id);

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

                // CustomerApprovedFinal: Actor'ın (customer) bildirimlerini otomatik okunmuş yap
                await notificationService.MarkReadByAppointmentIdAsync(customerUserId, appt.Id);

                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
            }

            await appointmentDal.Update(appt);

            if (!approve)
            {
                await UpdateThreadStoreOwnerAsync(appt.Id, null);
                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
            }
            // Notification payload update
            // Reject senaryosunda PendingExpiresAt overall 30dk'ya döner; bu durumda StoreApprovedSelection notification'ı
            // eski customer step PendingExpiresAt ile kayıtlı olduğu için önce onu güncelle.
            if (!approve && previousPendingExpiresAt.HasValue)
            {
                await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                    appt.Id,
                    appt.Status,
                    appt.StoreDecision,
                    appt.FreeBarberDecision,
                    appt.CustomerDecision,
                    previousPendingExpiresAt
                );
            }

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

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor


            // Customer kararını verdi, ilgili bildirimleri okundu olarak işaretle
            await notificationService.MarkReadByAppointmentIdAsync(customerUserId, appt.Id);

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- CANCEL / COMPLETE ----------------
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
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



            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledByUserId = userId;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            // FreeBarber'ı müsait yap
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCancelled, actorUserId: userId);

            // İptal sonrası slot kilidini kaldır (availability + unique index için)
            // ÖNEMLİ: Store bilgisini (BarberStoreUserId) silme.
            // Bildirim payload'ı dolu gelsin diye notify'dan SONRA temizliyoruz.
            if (appt.ChairId.HasValue)
            {
                appt.ChairId = null;
                appt.ManuelBarberId = null;
                await appointmentDal.Update(appt);
            }

            // 3'lü sistem (StoreSelection) durumunda FreeBarber'ın AppointmentCreated notification'ındaki reddet butonu kalkmalı
            // 2'li sistem (CustomRequest) durumunda bu kontrol gerekmiyor
            if (appt.StoreSelectionType == StoreSelectionType.StoreSelection)
            {
                await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                    appt.Id,
                    appt.Status,
                    appt.StoreDecision,
                    appt.FreeBarberDecision,
                    appt.CustomerDecision,
                    appt.PendingExpiresAt
                );
            }

            //await notificationService.MarkReadByAppointmentIdAsync(userId, appt.Id);



            // Thread g├╝ncellemesi (thread kald─▒r─▒lacak)
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);

            // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<bool>(true);
        }
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
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

            // Tamamlama sonrası slot kilidini kaldır (availability + unique index için)
            // ÖNEMLİ: Store bilgisini (BarberStoreUserId) silme.
            // Bildirim payload'ı dolu gelsin diye notify'dan SONRA temizliyoruz.
            if (appt.ChairId.HasValue)
            {
                appt.ChairId = null;
                appt.ManuelBarberId = null;
                await appointmentDal.Update(appt);
            }

            // 3'lü sistem (StoreSelection) durumunda FreeBarber'ın AppointmentCreated notification'ındaki reddet butonu kalkmalı
            // 2'li sistem (CustomRequest) durumunda bu kontrol gerekmiyor
            if (appt.StoreSelectionType == StoreSelectionType.StoreSelection)
            {
                await notificationService.UpdateNotificationPayloadByAppointmentAsync(
                    appt.Id,
                    appt.Status,
                    appt.StoreDecision,
                    appt.FreeBarberDecision,
                    appt.CustomerDecision,
                    appt.PendingExpiresAt
                );
            }



            // Thread g├╝ncellemesi (thread kald─▒r─▒lacak)
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);

            // ─░lgili kullan─▒c─▒lara appointment g├╝ncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<bool>(true);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> DeleteAsync(Guid userId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null)
                return new ErrorDataResult<bool>(false, Messages.AppointmentNotFound);

            // Kullanıcının bu appointment'ta participant olup olmadığını kontrol et
            var isParticipant =
                appt.CustomerUserId == userId ||
                appt.BarberStoreUserId == userId ||
                appt.FreeBarberUserId == userId;

            if (!isParticipant)
                return new ErrorDataResult<bool>(false, Messages.Unauthorized);

            // Pending veya Approved durumundaki randevular silinemez
            if (appt.Status == AppointmentStatus.Pending || appt.Status == AppointmentStatus.Approved)
                return new ErrorDataResult<bool>(false, Messages.CannotDeletePendingOrApproved);

            // Kullanıcının tipine göre ilgili soft delete flag'ini true yap
            if (appt.CustomerUserId == userId)
            {
                appt.IsDeletedByCustomerUserId = true;
            }
            else if (appt.BarberStoreUserId == userId)
            {
                appt.IsDeletedByBarberStoreUserId = true;
            }
            else if (appt.FreeBarberUserId == userId)
            {
                appt.IsDeletedByFreeBarberUserId = true;
            }

            appt.UpdatedAt = DateTime.UtcNow;
            await appointmentDal.Update(appt);

            // İlgili ChatThread'i bul ve kullanıcı için soft delete yap
            var thread = await threadDal.Get(t => t.AppointmentId == appt.Id);
            if (thread != null)
            {
                // Kullanıcının tipine göre thread soft delete
                if (appt.CustomerUserId == userId)
                {
                    thread.IsDeletedByCustomerUserId = true;
                    // Unread count'u sıfırla
                    thread.CustomerUnreadCount = 0;
                }
                else if (appt.BarberStoreUserId == userId)
                {
                    thread.IsDeletedByStoreOwnerUserId = true;
                    // Unread count'u sıfırla
                    thread.StoreUnreadCount = 0;
                }
                else if (appt.FreeBarberUserId == userId)
                {
                    thread.IsDeletedByFreeBarberUserId = true;
                    // Unread count'u sıfırla
                    thread.FreeBarberUnreadCount = 0;
                }

                thread.UpdatedAt = DateTime.UtcNow;
                await threadDal.Update(thread);

                // Thread removed push et (kullanıcı için)
                await realtime.PushChatThreadRemovedAsync(userId, thread.Id);

                // Badge count'u güncelle (thread unread count değişti)
                // Badge service thread unread count'ları da sayıyor, bu yüzden güncellememiz gerekiyor
                // Ancak AppointmentManager'da badgeService yok, bu yüzden chatService üzerinden yapabiliriz
                // veya notification service üzerinden badge güncellemesi yapılabilir
                // Şimdilik chat service'in PushChatThreadRemovedAsync'i badge'i de güncelliyor olabilir
                // Kontrol etmek gerekiyor, ama şimdilik bu şekilde bırakalım
            }

            // Eğer tüm kullanıcılar soft delete yaptıysa, appointment'ı hard delete et
            var allDeleted = true;
            if (appt.CustomerUserId.HasValue && !appt.IsDeletedByCustomerUserId)
                allDeleted = false;
            if (appt.BarberStoreUserId.HasValue && !appt.IsDeletedByBarberStoreUserId)
                allDeleted = false;
            if (appt.FreeBarberUserId.HasValue && !appt.IsDeletedByFreeBarberUserId)
                allDeleted = false;

            if (allDeleted)
            {
                // Tüm kullanıcılar soft delete yaptı, appointment'ı hard delete et
                await appointmentDal.Remove(appt);

                // Thread'i de hard delete et
                if (thread != null)
                {
                    await threadDal.Remove(thread);
                }

                // İlgili tüm kullanıcılara thread removed push et
                var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                foreach (var participantUserId in participantUserIds)
                {
                    await realtime.PushChatThreadRemovedAsync(participantUserId, thread!.Id);
                }
            }

            // Appointment güncellemesini bildir (kullanıcı için)
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

            return new SuccessDataResult<bool>(true);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> DeleteAllAsync(Guid userId)
        {
            // Kullanıcının tüm appointment'larını bul (soft delete edilmemiş olanlar)
            var appointments = await appointmentDal.GetAll(x =>
                (x.CustomerUserId == userId && !x.IsDeletedByCustomerUserId) ||
                (x.BarberStoreUserId == userId && !x.IsDeletedByBarberStoreUserId) ||
                (x.FreeBarberUserId == userId && !x.IsDeletedByFreeBarberUserId));

            if (appointments == null || !appointments.Any())
                return new ErrorDataResult<bool>(false, Messages.AppointmentNotFoundForDelete);

            var appointmentsToDelete = new List<Appointment>();
            var cannotDeleteCount = 0;

            foreach (var appt in appointments)
            {
                // Pending veya Approved durumundaki randevular silinemez
                if (appt.Status == AppointmentStatus.Pending || appt.Status == AppointmentStatus.Approved)
                {
                    cannotDeleteCount++;
                    continue;
                }

                appointmentsToDelete.Add(appt);

                // Kullanıcının tipine göre ilgili soft delete flag'ini true yap
                if (appt.CustomerUserId == userId)
                {
                    appt.IsDeletedByCustomerUserId = true;
                }
                else if (appt.BarberStoreUserId == userId)
                {
                    appt.IsDeletedByBarberStoreUserId = true;
                }
                else if (appt.FreeBarberUserId == userId)
                {
                    appt.IsDeletedByFreeBarberUserId = true;
                }

                appt.UpdatedAt = DateTime.UtcNow;
            }

            if (!appointmentsToDelete.Any() && cannotDeleteCount > 0)
            {
                return new ErrorDataResult<bool>(false, string.Format(Messages.NoAppointmentsDeleted, cannotDeleteCount));
            }
            else if (!appointmentsToDelete.Any() && cannotDeleteCount == 0)
            {
                return new ErrorDataResult<bool>(false, Messages.AppointmentNotFoundForDelete);
            }

            // Soft delete flag'lerini güncelle
            foreach (var appt in appointmentsToDelete)
            {
                await appointmentDal.Update(appt);
            }

            // Thread'leri bul ve güncelle
            var appointmentIds = appointmentsToDelete.Select(a => a.Id).ToList();
            var threads = await threadDal.GetAll(t => appointmentIds.Contains(t.AppointmentId!.Value));

            var threadsToUpdate = new List<ChatThread>();
            var threadsToDelete = new List<ChatThread>();

            foreach (var thread in threads)
            {
                var appt = appointmentsToDelete.FirstOrDefault(a => a.Id == thread.AppointmentId!.Value);
                if (appt == null) continue;

                // Kullanıcının tipine göre thread soft delete
                if (appt.CustomerUserId == userId)
                {
                    thread.IsDeletedByCustomerUserId = true;
                    thread.CustomerUnreadCount = 0;
                }
                else if (appt.BarberStoreUserId == userId)
                {
                    thread.IsDeletedByStoreOwnerUserId = true;
                    thread.StoreUnreadCount = 0;
                }
                else if (appt.FreeBarberUserId == userId)
                {
                    thread.IsDeletedByFreeBarberUserId = true;
                    thread.FreeBarberUnreadCount = 0;
                }

                thread.UpdatedAt = DateTime.UtcNow;
                threadsToUpdate.Add(thread);

                // Thread removed push et (kullanıcı için)
                await realtime.PushChatThreadRemovedAsync(userId, thread.Id);

                // Eğer tüm kullanıcılar thread'i soft delete yaptıysa, thread'i hard delete et
                var allThreadDeleted = true;
                if (appt.CustomerUserId.HasValue && !thread.IsDeletedByCustomerUserId)
                    allThreadDeleted = false;
                if (appt.BarberStoreUserId.HasValue && !thread.IsDeletedByStoreOwnerUserId)
                    allThreadDeleted = false;
                if (appt.FreeBarberUserId.HasValue && !thread.IsDeletedByFreeBarberUserId)
                    allThreadDeleted = false;

                if (allThreadDeleted)
                {
                    threadsToDelete.Add(thread);
                }
            }

            // Thread'leri güncelle
            foreach (var thread in threadsToUpdate)
            {
                await threadDal.Update(thread);
            }

            // Hard delete edilecek thread'leri sil
            foreach (var thread in threadsToDelete)
            {
                await threadDal.Remove(thread);

                // İlgili tüm kullanıcılara thread removed push et
                var appt = appointmentsToDelete.FirstOrDefault(a => a.Id == thread.AppointmentId!.Value);
                if (appt != null)
                {
                    var participantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .Distinct()
                        .ToList();

                    foreach (var participantUserId in participantUserIds)
                    {
                        await realtime.PushChatThreadRemovedAsync(participantUserId, thread.Id);
                    }
                }
            }

            // Tüm kullanıcılar soft delete yapan appointment'ları hard delete et
            var appointmentsToHardDelete = new List<Appointment>();
            foreach (var appt in appointmentsToDelete)
            {
                var allDeleted = true;
                if (appt.CustomerUserId.HasValue && !appt.IsDeletedByCustomerUserId)
                    allDeleted = false;
                if (appt.BarberStoreUserId.HasValue && !appt.IsDeletedByBarberStoreUserId)
                    allDeleted = false;
                if (appt.FreeBarberUserId.HasValue && !appt.IsDeletedByFreeBarberUserId)
                    allDeleted = false;

                if (allDeleted)
                {
                    appointmentsToHardDelete.Add(appt);
                }
            }

            // Hard delete edilecek appointment'ları sil
            foreach (var appt in appointmentsToHardDelete)
            {
                await appointmentDal.Remove(appt);
            }

            // Appointment güncellemelerini bildir (kullanıcı için)
            foreach (var appt in appointmentsToDelete)
            {
                await NotifyAppointmentUpdateToParticipantsAsync(appt);
            }

            // Transaction commit sonrası badge update'leri TransactionScopeAspect tarafından otomatik çalıştırılıyor

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
            // Service offerings snapshot - kritik, başarısız olursa exception fırlatılmalı
            await CreateAppointmentServiceOfferingsAsync(appt.Id, serviceOfferingIds);

            // Thread oluştur ve push et - kritik, başarısız olursa randevu oluşturulmamalı
            await EnsureThreadAndPushCreatedAsync(appt);

            // Notification gönder - kritik, başarısız olursa randevu oluşturulmamalı
            await notifySvc.NotifyWithAppointmentAsync(appt, NotificationType.AppointmentCreated, actorUserId: actorUserId);
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

            // Thread oluşturma kritik - başarısız olursa exception fırlatılır ve transaction rollback olur
            await threadDal.Add(thread);

            // Katılımcılara chat.threadCreated push
            // GetThreadsAsync mantığını kullanarak thread detaylarını doldur
            // Push işlemi başarısız olsa bile thread oluşturuldu, bu yüzden try-catch ile koruyoruz
            // Ancak thread oluşturma başarısız olursa exception fırlatılır
            try
            {
                await chatService.PushAppointmentThreadCreatedAsync(appt.Id);
            }
            catch
            {
                // Push başarısız olsa bile thread oluşturuldu, devam et
                // Thread zaten database'de, kullanıcılar refresh yaptığında görecek
                // Push işlemi kritik değil, thread oluşturma kritik
            }
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

                    return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
                }

                return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
            }

            appt.Status = AppointmentStatus.Unanswered;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            // Pending kararları NoAnswer'a çek (aksiyon engelleme + payload tutarlılığı)
            if (appt.StoreDecision == DecisionStatus.Pending)
                appt.StoreDecision = DecisionStatus.NoAnswer;
            if (appt.FreeBarberDecision == DecisionStatus.Pending)
                appt.FreeBarberDecision = DecisionStatus.NoAnswer;
            if (appt.CustomerDecision == DecisionStatus.Pending)
                appt.CustomerDecision = DecisionStatus.NoAnswer;

            // Önce status'u kaydet (bildirim payload'ı dolu gelsin)
            await appointmentDal.Update(appt);

            // FreeBarber'ı müsait yap
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            // Bildirim gönder
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);

            // Cevapsız sonrası slot kilidini kaldır (availability + unique index için)
            // ÖNEMLİ: Store bilgisini (BarberStoreUserId) silme.
            // Bildirim payload'ı dolu gelsin diye notify'dan SONRA temizliyoruz.
            if (appt.ChairId.HasValue)
            {
                appt.ChairId = null;
                appt.ManuelBarberId = null;
                await appointmentDal.Update(appt);
            }

            // Thread'i kaldır ve unread mesajları read yap
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);

            // Appointment güncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

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
                    // Thread kaldırılmadan önce tüm katılımcılar için unread mesajları read yap
                    // Bu sayede thread kaybolduğunda kullanıcılar okunmamış mesaj sayısı görmeyecek
                    thread.CustomerUnreadCount = 0;
                    thread.StoreUnreadCount = 0;
                    thread.FreeBarberUnreadCount = 0;
                    await threadDal.Update(thread);


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


                            continue;
                        }
                    }

                    // E─şer target filter'da bulunamad─▒ysa veya belirlenemediyse t├╝m filter'lar─▒ kontrol et
                    var allFilters = new[] { AppointmentFilter.Active, AppointmentFilter.Completed, AppointmentFilter.Cancelled };

                    foreach (var filter in allFilters)
                    {
                        if (targetFilter.HasValue && filter == targetFilter.Value)
                            continue; // Zaten kontrol ettik

                        var filterAppointments = await appointmentDal.GetAllAppointmentByFilter(userId, filter);
                        var updatedInFilter = filterAppointments.FirstOrDefault(a => a.Id == appt.Id);
                        
                        if (updatedInFilter != null)
                        {
                            await realtime.PushAppointmentUpdatedAsync(userId, updatedInFilter);
                            break;
                        }
                    }
                }
                catch
                {
                }
            }
        }


    }
}
























