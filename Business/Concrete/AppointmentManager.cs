using Business.Abstract;
using Business.Resources;
using Core.Aspect.Autofac.Transaction;
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
        IOptions<AppointmentSettings> appointmentSettings
    ) : IAppointmentService
    {
        private static readonly AppointmentStatus[] Active = [AppointmentStatus.Pending, AppointmentStatus.Approved];
        private readonly AppointmentSettings _settings = appointmentSettings.Value;

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

        // ---------------- CREATE: CUSTOMER -> STORE (+ optional FREEBARBER) ----------------

        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateCustomerToStoreAndFreeBarberControlAsync(Guid customerUserId, CreateAppointmentRequestDto req)
        {
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFoundEnglish);

            if (!req.ChairId.HasValue) return new ErrorDataResult<Guid>(Messages.ChairRequired);
            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>(Messages.StartTimeEndTimeRequired);

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>(Messages.StartTimeGreaterThanEndTime);

            // not past (TR)
            var pastRes = EnsureNotPast(req.AppointmentDate, start, bufferMinutes: 0);
            if (!pastRes.Success) return new ErrorDataResult<Guid>(pastRes.Message);

            // chair store'a ait mi?
            var chair = await chairDal.Get(c => c.Id == req.ChairId.Value && c.StoreId == req.StoreId);
            if (chair is null) return new ErrorDataResult<Guid>(Messages.ChairNotInStore);

            Guid? manuelBarberId = null;
            if (!req.FreeBarberUserId.HasValue && chair.ManuelBarberId.HasValue)
            {
                manuelBarberId = chair.ManuelBarberId.Value;
            }

            // store açık mı?
            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // chair overlap var mı?
            var overlapRes = await EnsureChairNoOverlapAsync(req.ChairId.Value, req.AppointmentDate, start, end);
            if (!overlapRes.Success) return new ErrorDataResult<Guid>(overlapRes.Message);

            if (!req.RequestLatitude.HasValue || !req.RequestLongitude.HasValue)
                return new ErrorDataResult<Guid>(Messages.LocationRequired);

            var customerLat = req.RequestLatitude.Value;
            var customerLon = req.RequestLongitude.Value;

            {
                // store zaten yukarıda "store" değişkeninde var, burada direkt kullan
                var distRes = EnsureWithinKm(customerLat, customerLon, store.Latitude, store.Longitude, MaxDistanceKm,
                    Messages.CustomerDistanceExceeded);
                if (!distRes.Success) return new ErrorDataResult<Guid>(distRes.Message);
            }

            FreeBarber? fbEntity = null;

            if (req.FreeBarberUserId.HasValue)
            {
                var fbRes = await GetFreeBarberCheckedAsync(req.FreeBarberUserId.Value, mustBeAvailable: true);
                if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);

                fbEntity = fbRes.Data;

                var distRes2 = EnsureWithinKm(customerLat, customerLon, fbEntity.Latitude, fbEntity.Longitude, MaxDistanceKm,
                    Messages.FreeBarberDistanceExceeded);
                if (!distRes2.Success) return new ErrorDataResult<Guid>(distRes2.Message);

                // 3) FreeBarber ↔ Store  ✅ (kritik)
                var distFbStore = EnsureWithinKm(fbEntity.Latitude, fbEntity.Longitude, store.Latitude, store.Longitude, MaxDistanceKm,
                    Messages.FreeBarberStoreDistanceExceeded);
                if (!distFbStore.Success) return new ErrorDataResult<Guid>(distFbStore.Message);
            }

            // active rules (customer & free barber single-active, store single active "call" rule)
            var rule = await EnforceActiveRules(customerUserId, req.FreeBarberUserId, store.BarberStoreOwnerId, AppointmentRequester.Customer);
            if (!rule.Success) return new ErrorDataResult<Guid>(rule.Message);

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = req.ChairId.Value,
                AppointmentDate = req.AppointmentDate,
                StartTime = start,
                EndTime = end,

                BarberStoreUserId = store.BarberStoreOwnerId,
                CustomerUserId = customerUserId,
                FreeBarberUserId = req.FreeBarberUserId, // null olabilir
                ManuelBarberId = manuelBarberId,
                RequestedBy = AppointmentRequester.Customer,
                Status = AppointmentStatus.Pending,

                StoreDecision = DecisionStatus.Pending,
                // FreeBarber yoksa otomatik Approved say
                FreeBarberDecision = req.FreeBarberUserId.HasValue ? DecisionStatus.Pending : DecisionStatus.Approved,

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
                // Unique constraint violation (aynı chair/date/start/end kombinasyonu)
                // Overlap kontrolü zaten EnsureChairNoOverlapAsync'te yapıldı
                // Bu exception genellikle race condition durumunda oluşur
                // (iki kullanıcı aynı anda aynı slot'u seçtiğinde)
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            // offerings snapshot - AddRange ile toplu ekleme
            await CreateAppointmentServiceOfferingsAsync(appt.Id, req.ServiceOfferingIds);

            // FREEBARBER LOCK
            if (fbEntity is not null)
            {
                var lockRes = await SetFreeBarberAvailabilityAsync(fbEntity, false);
                if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);
            }

            // thread + threadCreated push
            await EnsureThreadAndPushCreatedAsync(appt);

            // notify: created (appointment entity'sini direkt geçiyoruz - transaction içinde olduğu için)
            var result = await notifySvc.NotifyWithAppointmentAsync(appt, NotificationType.AppointmentCreated, actorUserId: customerUserId);

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- CREATE: FREEBARBER -> STORE ----------------
        [TransactionScopeAspect]

        public async Task<IDataResult<Guid>> CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req)
        {
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFoundEnglish);

            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>(Messages.StartTimeEndTimeRequired);

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>(Messages.StartTimeGreaterThanEndTime);

            var pastRes = EnsureNotPast(req.AppointmentDate, start, bufferMinutes: 0);
            if (!pastRes.Success) return new ErrorDataResult<Guid>(pastRes.Message);

            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // freebarber must be available
            var fbRes = await GetFreeBarberCheckedAsync(freeBarberUserId, mustBeAvailable: true);
            if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);

            var fb = fbRes.Data;

            var distRes = EnsureWithinKm(fb.Latitude, fb.Longitude, store.Latitude, store.Longitude, MaxDistanceKm,
                Messages.FreeBarberStoreDistanceExceeded);
            if (!distRes.Success) return new ErrorDataResult<Guid>(distRes.Message);

            // chair seçilmişse store’a ait + overlap kontrol
            if (req.ChairId.HasValue)
            {
                var chair = await chairDal.Get(c => c.Id == req.ChairId.Value && c.StoreId == req.StoreId);
                if (chair is null) return new ErrorDataResult<Guid>(Messages.ChairNotInStore);

                var overlapRes = await EnsureChairNoOverlapAsync(req.ChairId.Value, req.AppointmentDate, start, end);
                if (!overlapRes.Success) return new ErrorDataResult<Guid>(overlapRes.Message);
            }

            var rule = await EnforceActiveRules(customerId: null, freeBarberId: freeBarberUserId, storeOwnerId: store.BarberStoreOwnerId, AppointmentRequester.FreeBarber);
            if (!rule.Success) return new ErrorDataResult<Guid>(rule.Message);

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = req.ChairId,

                BarberStoreUserId = store.BarberStoreOwnerId,
                CustomerUserId = null,
                FreeBarberUserId = freeBarberUserId,
                ManuelBarberId = null,
                AppointmentDate = req.AppointmentDate,
                StartTime = start,
                EndTime = end,

                RequestedBy = AppointmentRequester.FreeBarber,
                Status = AppointmentStatus.Pending,

                // requester otomatik "evet"
                FreeBarberDecision = DecisionStatus.Approved,
                StoreDecision = DecisionStatus.Pending,

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
                // Unique constraint violation - overlap kontrolü zaten yapıldı
                // Bu exception genellikle race condition durumunda oluşur
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            // offerings snapshot - AddRange ile toplu ekleme
            await CreateAppointmentServiceOfferingsAsync(appt.Id, req.ServiceOfferingIds);

            // lock free barber
            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await EnsureThreadAndPushCreatedAsync(appt);

            // notify: created (appointment entity'sini direkt geçiyoruz - transaction içinde olduğu için)
            await notifySvc.NotifyWithAppointmentAsync(appt, NotificationType.AppointmentCreated, actorUserId: freeBarberUserId);

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- CREATE: STORE -> FREEBARBER (CALL) ----------------
        [TransactionScopeAspect]

        public async Task<IDataResult<Guid>> CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateAppointmentRequestDto req)
        {
            if (!req.FreeBarberUserId.HasValue) return new ErrorDataResult<Guid>(Messages.FreeBarberUserIdRequired);
            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>(Messages.StartTimeEndTimeRequired);

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>(Messages.StartTimeGreaterThanEndTime);

            var store = await barberStoreDal.Get(x => x.Id == req.StoreId && x.BarberStoreOwnerId == storeOwnerUserId);
            if (store is null) return new ErrorDataResult<Guid>(Messages.StoreNotFoundOrNotOwner);

            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // freebarber only availability
            var fbRes = await GetFreeBarberCheckedAsync(req.FreeBarberUserId.Value, mustBeAvailable: true);
            if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);

            var fb = fbRes.Data;

            var distRes = EnsureWithinKm(store.Latitude, store.Longitude, fb.Latitude, fb.Longitude, MaxDistanceKm,
                Messages.StoreFreeBarberDistanceExceeded);
            if (!distRes.Success) return new ErrorDataResult<Guid>(distRes.Message);


            // store aynı anda sadece 1 active "call" kuralı
            var rule = await EnforceActiveRules(customerId: null, freeBarberId: req.FreeBarberUserId.Value, storeOwnerId: storeOwnerUserId, AppointmentRequester.Store);
            if (!rule.Success) return new ErrorDataResult<Guid>(rule.Message);

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = req.ChairId, // opsiyonel (istersen zorunlu yaparsın)

                BarberStoreUserId = storeOwnerUserId,
                CustomerUserId = null,
                FreeBarberUserId = req.FreeBarberUserId.Value,
                ManuelBarberId = null,
                AppointmentDate = req.AppointmentDate,
                StartTime = start,
                EndTime = end,

                RequestedBy = AppointmentRequester.Store,
                Status = AppointmentStatus.Pending,

                StoreDecision = DecisionStatus.Approved,    // requester otomatik
                FreeBarberDecision = DecisionStatus.Pending,

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
                // Unique constraint violation - overlap kontrolü zaten yapıldı
                // Bu exception genellikle race condition durumunda oluşur
                return new ErrorDataResult<Guid>(Messages.AppointmentSlotTaken);
            }

            // offerings snapshot - AddRange ile toplu ekleme
            await CreateAppointmentServiceOfferingsAsync(appt.Id, req.ServiceOfferingIds);

            // lock free barber
            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await EnsureThreadAndPushCreatedAsync(appt);

            // notify: created (appointment entity'sini direkt geçiyoruz - transaction içinde olduğu için)
            await notifySvc.NotifyWithAppointmentAsync(appt, NotificationType.AppointmentCreated, actorUserId: storeOwnerUserId);

            return new SuccessDataResult<Guid>(appt.Id);
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

            // ekstra: aynı taraf tekrar karar veremesin
            if (appt.StoreDecision != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);

            appt.StoreDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                appt.Status = AppointmentStatus.Rejected;
                appt.PendingExpiresAt = null;
            }
            else
            {
                // freebarber yoksa FreeBarberDecision zaten Approved -> direkt Approved olur
                if (appt.FreeBarberDecision == DecisionStatus.Approved)
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
                appt.FreeBarberDecision
            );

            // Decision verildikten sonra ilgili notification'ları read yap
            // Decision button'ları gizlemek için notification'ları okundu yapıyoruz
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

            // hala pending (örn: freebarber bekleniyor)
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentDecisionUpdated, actorUserId: storeOwnerUserId,
                extra: new { storeDecision = appt.StoreDecision, freeBarberDecision = appt.FreeBarberDecision });
            
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

            if (appt.FreeBarberDecision != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, Messages.AppointmentDecisionAlreadyGiven);

            appt.FreeBarberDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            if (!approve)
            {
                appt.Status = AppointmentStatus.Rejected;
                appt.PendingExpiresAt = null;
            }
            else
            {
                if (appt.StoreDecision == DecisionStatus.Approved)
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
                appt.FreeBarberDecision
            );

            // Decision verildikten sonra ilgili notification'ları read yap
            // Decision button'ları gizlemek için notification'ları okundu yapıyoruz
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
                
                return new SuccessDataResult<bool>(true);
            }

            // hala pending (örn: store bekleniyor)
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentDecisionUpdated, actorUserId: freeBarberUserId,
                extra: new { storeDecision = appt.StoreDecision, freeBarberDecision = appt.FreeBarberDecision });
            
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

            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledByUserId = userId;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            // İptal edildiğinde ilgili tüm taraflara bildirim gönder
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCancelled, actorUserId: userId);
            
            // İptal durumunda chat mesajı gönder
            try
            {
                var cancelMessage = "Randevu iptal edildi.";
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

            return new SuccessDataResult<bool>(true);
        }
        [TransactionScopeAspect]

        public async Task<IDataResult<bool>> CompleteAsync(Guid storeOwnerUserId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(Messages.AppointmentNotFound);
            
            // Sadece berber (store owner) randevuyu tamamlayabilir
            if (appt.BarberStoreUserId != storeOwnerUserId) return new ErrorDataResult<bool>(Messages.Unauthorized);
            
            if (appt.Status != AppointmentStatus.Approved) return new ErrorDataResult<bool>(Messages.AppointmentNotApproved);

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

            appt.Status = AppointmentStatus.Completed;
            appt.CompletedAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            // FreeBarber müsaitliğini serbest bırak
            // Completed durumunda serbest berberi müsait yap
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCompleted, actorUserId: storeOwnerUserId);
            
            // Tamamlanma durumunda chat mesajı gönder
            try
            {
                var completeMessage = "Randevu tamamlandı.";
                await chatService.SendMessageAsync(storeOwnerUserId, appt.Id, completeMessage);
            }
            catch
            {
                // Chat mesajı gönderilemezse devam et, kritik değil
            }
            
            // Thread güncellemesi (thread kaldırılacak)
            await UpdateThreadOnAppointmentStatusChangeAsync(appt);
            
            // İlgili kullanıcılara appointment güncellemesini bildir
            await NotifyAppointmentUpdateToParticipantsAsync(appt);

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- RULES / HELPERS ----------------

        private async Task<IResult> EnforceActiveRules(Guid? customerId, Guid? freeBarberId, Guid? storeOwnerId, AppointmentRequester requestedBy)
        {
            // NOTE: Race condition riski var - EnforceActiveRules ile Add() arasında
            // Database seviyesinde unique constraint'ler (IX_Appointments) bu durumu önler
            // Transaction isolation level Serializable kullanılabilir ama performans etkisi olabilir
            // Şu an için database constraint'ler yeterli koruma sağlıyor
            
            if (customerId.HasValue)
            {
                var has = await appointmentDal.AnyAsync(x => x.CustomerUserId == customerId && Active.Contains(x.Status));
                if (has) return new ErrorResult(Messages.CustomerHasActiveAppointment);
            }

            if (freeBarberId.HasValue)
            {
                var has = await appointmentDal.AnyAsync(x => x.FreeBarberUserId == freeBarberId && Active.Contains(x.Status));
                if (has) return new ErrorResult(Messages.FreeBarberHasActiveAppointment);
            }

            // Store aynı anda sadece 1 aktif "call" (Store->FreeBarber) yapsın
            if (requestedBy == AppointmentRequester.Store && storeOwnerId.HasValue && freeBarberId.HasValue)
            {
                var has = await appointmentDal.AnyAsync(x =>
                    x.BarberStoreUserId == storeOwnerId &&
                    x.RequestedBy == AppointmentRequester.Store &&
                    x.CustomerUserId == null &&
                    x.FreeBarberUserId != null &&
                    Active.Contains(x.Status));

                if (has) return new ErrorResult(Messages.StoreHasActiveCall);
            }

            return new SuccessResult();
        }

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

        // FreeBarber table lookup by FreeBarberUserId
        private IResult EnsureNotPast(DateOnly date, TimeSpan start, int bufferMinutes = 0)
        {
            var nowTr = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var today = DateOnly.FromDateTime(nowTr);

            if (date < today)
                return new ErrorResult(Messages.AppointmentPastDate);

            if (date == today)
            {
                var nowTime = nowTr.TimeOfDay.Add(TimeSpan.FromMinutes(bufferMinutes));
                if (start <= nowTime)
                    return new ErrorResult(Messages.AppointmentPastTime);
            }

            return new SuccessResult();
        }

        private IDataResult<DateTime> GetAppointmentStartTr(Appointment appt)
        {
            try
            {
                // DateOnly + TimeSpan -> DateTime (TR local kabul)
                var startLocal = appt.AppointmentDate.ToDateTime(TimeOnly.FromTimeSpan(appt.StartTime));

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
                // DateOnly + TimeSpan -> DateTime (TR local kabul)
                var endLocal = appt.AppointmentDate.ToDateTime(TimeOnly.FromTimeSpan(appt.EndTime));

                // local time (TR) olarak DateTime döndürüyoruz
                // (DateTime.Now ile kıyas için)
                return new SuccessDataResult<DateTime>(endLocal);
            }
            catch
            {
                return new ErrorDataResult<DateTime>(Messages.AppointmentEndTimeCalculationFailed);
            }
        }

        private async Task<IResult> SetFreeBarberAvailabilityAsync(Guid freeBarberUserId, bool isAvailable)
        {
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            if (fb is null) return new ErrorResult(Messages.FreeBarberNotFound);

            fb.IsAvailable = isAvailable;
            fb.UpdatedAt = DateTime.UtcNow;

            await freeBarberDal.Update(fb);
            return new SuccessResult();
        }

        private async Task ReleaseFreeBarberIfNeededAsync(Guid? freeBarberUserId)
        {
            if (!freeBarberUserId.HasValue) return;
            await SetFreeBarberAvailabilityAsync(freeBarberUserId.Value, true);
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

        private IResult EnsureWithinKm(double fromLat, double fromLon, double toLat, double toLon, double maxKm, string msg)
        {
            var v1 = EnsureValidCoords(fromLat, fromLon, "İstek");
            if (!v1.Success) return v1;

            var v2 = EnsureValidCoords(toLat, toLon, "Hedef");
            if (!v2.Success) return v2;

            var km = HaversineKm(fromLat, fromLon, toLat, toLon);
            if (km > maxKm) return new ErrorResult($"{msg} (Mesafe: {km:0.00} km)");
            return new SuccessResult();
        }

        private async Task<IDataResult<FreeBarber>> GetFreeBarberCheckedAsync(Guid freeBarberUserId, bool mustBeAvailable = true)
        {
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            if (fb is null)
                return new ErrorDataResult<FreeBarber>(Messages.FreeBarberNotFound);

            var v = EnsureValidCoords(fb.Latitude, fb.Longitude, "Serbest berber");
            if (!v.Success)
                return new ErrorDataResult<FreeBarber>(v.Message);

            if (mustBeAvailable && !fb.IsAvailable)
                return new ErrorDataResult<FreeBarber>(Messages.FreeBarberNotAvailable);

            return new SuccessDataResult<FreeBarber>(fb);
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
            // PendingExpiresAt null ise (ör: eski kayıtlar) istersen “expire yok” kabul edebilirsin.
            if (appt.PendingExpiresAt.HasValue && appt.PendingExpiresAt.Value <= DateTime.UtcNow)
            {
                appt.Status = AppointmentStatus.Unanswered;
                appt.PendingExpiresAt = null;
                appt.UpdatedAt = DateTime.UtcNow;

                await appointmentDal.Update(appt);

                await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);

                return new ErrorDataResult<bool>(false, Messages.AppointmentTimeoutExpired);
            }

            return new SuccessDataResult<bool>(true);
        }

        // Helper: Randevu durumu değiştiğinde thread güncellemesi yap
        private async Task UpdateThreadOnAppointmentStatusChangeAsync(Appointment appt)
        {
            if (appt.Id == Guid.Empty) return;

            // Thread'i bul
            var thread = await threadDal.Get(t => t.AppointmentId == appt.Id);
            if (thread == null) return;

            // Durum artık Pending/Approved değilse thread'i kaldır
            if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Approved)
            {
                // Katılımcılara thread kaldırıldığını bildir
                var participants = new[] { thread.CustomerUserId, thread.StoreOwnerUserId, thread.FreeBarberUserId }
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                foreach (var userId in participants)
                {
                    await realtime.PushChatThreadRemovedAsync(userId, thread.Id);
                }
            }
            else
            {
                // Durum hala Pending/Approved ise thread'i güncelle (status değişmiş olabilir)
                // PushAppointmentThreadUpdatedAsync ile thread güncellemesini gönder
                // Bu metod tüm katılımcılara thread update push eder
                await chatService.PushAppointmentThreadUpdatedAsync(appt.Id);
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
                            var badgeSvc = (IBadgeService)realtime.GetType().GetProperty("BadgeService")?.GetValue(realtime);
                            if (badgeSvc == null)
                            {
                                // BadgeService'i dependency injection'dan al
                                // Burada AppointmentManager'da IBadgeService inject edilmeli
                                // Şimdilik NotificationManager üzerinden badge güncellemesi yapılacak
                            }
                            continue;
                        }
                    }
                    
                    // Eğer target filter'da bulunamadıysa veya belirlenemediyse tüm filter'ları kontrol et
                    var allFilters = new[] { AppointmentFilter.Active, AppointmentFilter.Completed, AppointmentFilter.Cancelled };
                    bool found = false;
                    
                    foreach (var filter in allFilters)
                    {
                        if (targetFilter.HasValue && filter == targetFilter.Value)
                            continue; // Zaten kontrol ettik
                            
                        var appointments = await appointmentDal.GetAllAppointmentByFilter(userId, filter);
                        var updatedAppt = appointments.FirstOrDefault(a => a.Id == appt.Id);
                        
                        if (updatedAppt != null)
                        {
                            await realtime.PushAppointmentUpdatedAsync(userId, updatedAppt);
                            found = true;
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
