using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

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
        IRealTimePublisher realtime
    ) : IAppointmentService
    {
        private static readonly AppointmentStatus[] Active = [AppointmentStatus.Pending, AppointmentStatus.Approved];

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
            if (store is null) return new ErrorDataResult<bool>(false, "Store not found");

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

        // ---------------- CREATE: CUSTOMER -> STORE (+ optional FREEBARBER) ----------------

        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateCustomerToStoreAndFreeBarberControlAsync(Guid customerUserId, CreateAppointmentRequestDto req)
        {
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>("Store not found");

            if (!req.ChairId.HasValue) return new ErrorDataResult<Guid>("ChairId is required");
            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>("StartTime/EndTime is required");

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>("Başlangıç saati bitişten büyük/eşit olamaz.");

            // not past (TR)
            var pastRes = EnsureNotPast(req.AppointmentDate, start, bufferMinutes: 0);
            if (!pastRes.Success) return new ErrorDataResult<Guid>(pastRes.Message);

            // chair store’a ait mi?
            var chair = await chairDal.Get(c => c.Id == req.ChairId.Value && c.StoreId == req.StoreId);
            if (chair is null) return new ErrorDataResult<Guid>("Chair not found in store");

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
                return new ErrorDataResult<Guid>("Konum bilgisi gerekli (RequestLatitude/RequestLongitude).");

            var customerLat = req.RequestLatitude.Value;
            var customerLon = req.RequestLongitude.Value;

            {
                // store zaten yukarıda "store" değişkeninde var, burada direkt kullan
                var distRes = EnsureWithinKm(customerLat, customerLon, store.Latitude, store.Longitude, MaxDistanceKm,
                    "Dükkan 1 km dışında. Yakın değilken randevu oluşturamazsın.");
                if (!distRes.Success) return new ErrorDataResult<Guid>(distRes.Message);
            }

            FreeBarber? fbEntity = null;

            if (req.FreeBarberUserId.HasValue)
            {
                var fbRes = await GetFreeBarberCheckedAsync(req.FreeBarberUserId.Value, mustBeAvailable: true);
                if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);

                fbEntity = fbRes.Data;

                var distRes2 = EnsureWithinKm(customerLat, customerLon, fbEntity.Latitude, fbEntity.Longitude, MaxDistanceKm,
                    "Serbest berber 1 km dışında. Yakın değilken randevu oluşturamazsın.");
                if (!distRes2.Success) return new ErrorDataResult<Guid>(distRes2.Message);

                // 3) FreeBarber ↔ Store  ✅ (kritik)
                var distFbStore = EnsureWithinKm(fbEntity.Latitude, fbEntity.Longitude, store.Latitude, store.Longitude, MaxDistanceKm,
                    "Serbest berber ile dükkan arası 1 km dışında. Bu eşleşmeyle randevu açılamaz.");
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

                PendingExpiresAt = DateTime.UtcNow.AddMinutes(5),
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
                // Son kontrol: overlap tekrar kontrol et
                var finalOverlap = await EnsureChairNoOverlapAsync(req.ChairId.Value, req.AppointmentDate, start, end);
                if (!finalOverlap.Success)
                    return new ErrorDataResult<Guid>(finalOverlap.Message);
                
                // Eğer hala overlap yoksa, başka bir unique constraint ihlali olabilir
                return new ErrorDataResult<Guid>("Bu randevu zamanı başka bir kullanıcı tarafından alındı. Lütfen başka bir saat seçin.");
            }

            // offerings snapshot
            if (req.ServiceOfferingIds.Count > 0)
            {
                var offs = await offeringDal.GetServiceOfferingsByIdsAsync(req.ServiceOfferingIds);
                foreach (var o in offs)
                {
                    await apptOfferingDal.Add(new AppointmentServiceOffering
                    {
                        Id = Guid.NewGuid(),
                        AppointmentId = appt.Id,
                        ServiceOfferingId = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    });
                }
            }

            // FREEBARBER LOCK
            if (fbEntity is not null)
            {
                var lockRes = await SetFreeBarberAvailabilityAsync(fbEntity, false);
                if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);
            }

            // thread + threadCreated push
            await EnsureThreadAndPushCreatedAsync(appt);

            // notify: created
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCreated, actorUserId: customerUserId);

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- CREATE: FREEBARBER -> STORE ----------------

        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req)
        {
            var store = await barberStoreDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>("Store not found");

            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>("StartTime/EndTime is required");

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>("Başlangıç saati bitişten büyük/eşit olamaz.");

            var pastRes = EnsureNotPast(req.AppointmentDate, start, bufferMinutes: 0);
            if (!pastRes.Success) return new ErrorDataResult<Guid>(pastRes.Message);

            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // freebarber must be available
            var fbRes = await GetFreeBarberCheckedAsync(freeBarberUserId, mustBeAvailable: true);
            if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);

            var fb = fbRes.Data;

            var distRes = EnsureWithinKm(fb.Latitude, fb.Longitude, store.Latitude, store.Longitude, MaxDistanceKm,
                "Serbest berber ile dükkan arası 1 km dışında. Bu şekilde randevu açılamaz.");
            if (!distRes.Success) return new ErrorDataResult<Guid>(distRes.Message);

            // chair seçilmişse store’a ait + overlap kontrol
            if (req.ChairId.HasValue)
            {
                var chair = await chairDal.Get(c => c.Id == req.ChairId.Value && c.StoreId == req.StoreId);
                if (chair is null) return new ErrorDataResult<Guid>("Koltuk dükkanda bulunamadı.");

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

                PendingExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await appointmentDal.Add(appt);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2627)
            {
                if (req.ChairId.HasValue)
                {
                    var finalOverlap = await EnsureChairNoOverlapAsync(req.ChairId.Value, req.AppointmentDate, start, end);
                    if (!finalOverlap.Success)
                        return new ErrorDataResult<Guid>(finalOverlap.Message);
                }
                return new ErrorDataResult<Guid>("Bu randevu zamanı başka bir kullanıcı tarafından alındı. Lütfen başka bir saat seçin.");
            }

            // lock free barber
            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await EnsureThreadAndPushCreatedAsync(appt);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCreated, actorUserId: freeBarberUserId);

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- CREATE: STORE -> FREEBARBER (CALL) ----------------

        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateAppointmentRequestDto req)
        {
            if (!req.FreeBarberUserId.HasValue) return new ErrorDataResult<Guid>("FreeBarberUserId is required");
            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>("StartTime/EndTime is required");

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>("Başlangıç saati bitişten büyük/eşit olamaz.");

            var store = await barberStoreDal.Get(x => x.Id == req.StoreId && x.BarberStoreOwnerId == storeOwnerUserId);
            if (store is null) return new ErrorDataResult<Guid>("Store not found or not owner");

            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // freebarber only availability
            var fbRes = await GetFreeBarberCheckedAsync(req.FreeBarberUserId.Value, mustBeAvailable: true);
            if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);

            var fb = fbRes.Data;

            var distRes = EnsureWithinKm(store.Latitude, store.Longitude, fb.Latitude, fb.Longitude, MaxDistanceKm,
                "Dükkan ile serbest berber arası 1 km dışında. Çağrı/randevu açılamaz.");
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

                PendingExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await appointmentDal.Add(appt);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2627)
            {
                if (req.ChairId.HasValue)
                {
                    var finalOverlap = await EnsureChairNoOverlapAsync(req.ChairId.Value, req.AppointmentDate, start, end);
                    if (!finalOverlap.Success)
                        return new ErrorDataResult<Guid>(finalOverlap.Message);
                }
                return new ErrorDataResult<Guid>("Bu randevu zamanı başka bir kullanıcı tarafından alındı. Lütfen başka bir saat seçin.");
            }

            // lock free barber
            var lockRes = await SetFreeBarberAvailabilityAsync(fb, false);
            if (!lockRes.Success) return new ErrorDataResult<Guid>(lockRes.Message);

            await EnsureThreadAndPushCreatedAsync(appt);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCreated, actorUserId: storeOwnerUserId);

            return new SuccessDataResult<Guid>(appt.Id);
        }

        // ---------------- DECISIONS (STORE / FREEBARBER) ----------------

        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> StoreDecisionAsync(Guid storeOwnerUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, "Randevu bulunamadı");
            if (appt.BarberStoreUserId != storeOwnerUserId) return new ErrorDataResult<bool>(false, "Yetki yok");
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>(false, "Bekleme yok");
            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;

            // ekstra: aynı taraf tekrar karar veremesin
            if (appt.StoreDecision != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, "Dükkan kararı zaten verilmiş.");

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

            if (appt.Status == AppointmentStatus.Rejected)
            {
                await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: storeOwnerUserId);
                return new SuccessDataResult<bool>(true);
            }

            if (appt.Status == AppointmentStatus.Approved)
            {
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: storeOwnerUserId);
                return new SuccessDataResult<bool>(true);
            }

            // hala pending (örn: freebarber bekleniyor)
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentDecisionUpdated, actorUserId: storeOwnerUserId,
                extra: new { storeDecision = appt.StoreDecision, freeBarberDecision = appt.FreeBarberDecision });

            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> FreeBarberDecisionAsync(Guid freeBarberUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>("Randevu bulunamadı");
            if (appt.FreeBarberUserId != freeBarberUserId) return new ErrorDataResult<bool>("Yetki yok");
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>("Beklemede değil");

            var exp = await EnsurePendingNotExpiredAndHandleAsync(appt);
            if (!exp.Success) return exp;

            if (appt.FreeBarberDecision != DecisionStatus.Pending)
                return new ErrorDataResult<bool>(false, "Serbest berber kararı zaten verilmiş.");

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

            if (appt.Status == AppointmentStatus.Rejected)
            {
                await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentRejected, actorUserId: freeBarberUserId);
                return new SuccessDataResult<bool>(true);
            }

            if (appt.Status == AppointmentStatus.Approved)
            {
                await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentApproved, actorUserId: freeBarberUserId);
                return new SuccessDataResult<bool>(true);
            }

            // hala pending (örn: store bekleniyor)
            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentDecisionUpdated, actorUserId: freeBarberUserId,
                extra: new { storeDecision = appt.StoreDecision, freeBarberDecision = appt.FreeBarberDecision });

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- CANCEL / COMPLETE ----------------

        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> CancelAsync(Guid userId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, "Randevu bulunamadı");

            var isParticipant =
                appt.CustomerUserId == userId ||
                appt.FreeBarberUserId == userId ||
                appt.BarberStoreUserId == userId;

            if (!isParticipant) return new ErrorDataResult<bool>(false, "Yetki yok");

            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<bool>(false, "İptal edilemez");

            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledByUserId = userId;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCancelled, actorUserId: userId);

            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> CompleteAsync(Guid storeOwnerUserId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>("Randevu bulunamadı");
            if (appt.BarberStoreUserId != storeOwnerUserId) return new ErrorDataResult<bool>("Yetki yok");
            if (appt.Status != AppointmentStatus.Approved) return new ErrorDataResult<bool>("Kabul edilmemiş randevu");

            // TR saati ile "bitti mi?"
            var endTrRes = GetAppointmentEndTr(appt);
            if (!endTrRes.Success) return new ErrorDataResult<bool>(endTrRes.Message);

            if (DateTime.Now < endTrRes.Data) // endTrRes TR local dönüyor
                return new ErrorDataResult<bool>("Randevu süresi dolmadan tamamlanamaz");

            appt.Status = AppointmentStatus.Completed;
            appt.CompletedAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);

            // FreeBarber müsaitliğini serbest bırak
            await ReleaseFreeBarberIfNeededAsync(appt.FreeBarberUserId);

            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentCompleted, actorUserId: storeOwnerUserId);

            return new SuccessDataResult<bool>(true);
        }

        // ---------------- RULES / HELPERS ----------------

        private async Task<IResult> EnforceActiveRules(Guid? customerId, Guid? freeBarberId, Guid? storeOwnerId, AppointmentRequester requestedBy)
        {
            if (customerId.HasValue)
            {
                var has = await appointmentDal.AnyAsync(x => x.CustomerUserId == customerId && Active.Contains(x.Status));
                if (has) return new ErrorResult("Müşterinin aktif (Pending/Approved) randevusu var.");
            }

            if (freeBarberId.HasValue)
            {
                var has = await appointmentDal.AnyAsync(x => x.FreeBarberUserId == freeBarberId && Active.Contains(x.Status));
                if (has) return new ErrorResult("Serbest berberin aktif (Pending/Approved) randevusu var.");
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

                if (has) return new ErrorResult("Dükkanın aktif bir serbest berber çağrısı var. Önce onu sonuçlandır.");
            }

            return new SuccessResult();
        }

        private async Task<IResult> EnsureChairNoOverlapAsync(Guid chairId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            var hasOverlap = await appointmentDal.AnyAsync(x =>
                x.ChairId == chairId &&
                x.AppointmentDate == date &&
                (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved) &&
                x.StartTime < end &&
                x.EndTime > start);

            return hasOverlap
                ? new ErrorResult("Bu koltuk için seçilen saat aralığında başka bir randevu var.")
                : new SuccessResult();
        }

        private async Task<IResult> EnsureStoreIsOpenAsync(Guid storeId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            var dow = date.DayOfWeek;

            var wh = await workingHourDal.Get(x =>
                x.OwnerId == storeId &&
                x.DayOfWeek == dow);

            if (wh is null)
                return new ErrorResult("Dükkan bu gün için çalışma saati tanımlamamış (kapalı).");

            if (wh.IsClosed)
                return new ErrorResult("Dükkan bu gün kapalı (tatil).");

            if (wh.StartTime > start || wh.EndTime < end)
                return new ErrorResult("Dükkan bu saat aralığında açık değil.");

            return new SuccessResult();
        }

        // FreeBarber table lookup by FreeBarberUserId
        private IResult EnsureNotPast(DateOnly date, TimeSpan start, int bufferMinutes = 0)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            var nowTr = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            var today = DateOnly.FromDateTime(nowTr);

            if (date < today)
                return new ErrorResult("Geçmiş tarih için randevu alınamaz.");

            if (date == today)
            {
                var nowTime = nowTr.TimeOfDay.Add(TimeSpan.FromMinutes(bufferMinutes));
                if (start <= nowTime)
                    return new ErrorResult("Geçmiş saat için randevu alınamaz.");
            }

            return new SuccessResult();
        }

        private IDataResult<DateTime> GetAppointmentEndTr(Appointment appt)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

                // DateOnly + TimeSpan -> DateTime (TR local kabul)
                var endLocal = appt.AppointmentDate.ToDateTime(TimeOnly.FromTimeSpan(appt.EndTime));

                // local time (TR) olarak DateTime döndürüyoruz
                // (DateTime.Now ile kıyas için)
                return new SuccessDataResult<DateTime>(endLocal);
            }
            catch
            {
                return new ErrorDataResult<DateTime>("Randevu bitiş zamanı hesaplanamadı.");
            }
        }

        private async Task<IResult> SetFreeBarberAvailabilityAsync(Guid freeBarberUserId, bool isAvailable)
        {
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            if (fb is null) return new ErrorResult("Serbest berber bulunamadı.");

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
            var existing = (await threadDal.GetAll(t => t.AppointmentId == appt.Id)).FirstOrDefault();
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
            var recipients = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

            // Store name (title için)
            var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId);

            foreach (var u in recipients)
            {
                var title = BuildThreadTitleForUser(u, appt, store?.StoreName);

                var dto = new ChatThreadListItemDto
                {
                    AppointmentId = appt.Id,
                    Status = appt.Status,
                    Title = title,
                    LastMessagePreview = null,
                    LastMessageAt = null,
                    UnreadCount = 0
                };

                await realtime.PushChatThreadCreatedAsync(u, dto);
            }
        }

        private static string BuildThreadTitleForUser(Guid userId, Appointment appt, string? storeName)
        {
            if (appt.BarberStoreUserId == userId)
            {
                // store owner kendi listesinde karşı taraf
                return appt.CustomerUserId.HasValue ? "Müşteri" : "Serbest Berber";
            }

            // customer/freebarber tarafı store’u görsün
            return string.IsNullOrWhiteSpace(storeName) ? "Berber Dükkanı" : storeName!;
        }

        private const double MaxDistanceKm = 1.0;

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
                return new ErrorDataResult<FreeBarber>("Serbest berber bulunamadı.");

            var v = EnsureValidCoords(fb.Latitude, fb.Longitude, "Serbest berber");
            if (!v.Success)
                return new ErrorDataResult<FreeBarber>(v.Message);

            if (mustBeAvailable && !fb.IsAvailable)
                return new ErrorDataResult<FreeBarber>("Serbest berber şu an müsait değil.");

            return new SuccessDataResult<FreeBarber>(fb);
        }

        private async Task<IResult> SetFreeBarberAvailabilityAsync(FreeBarber fb, bool isAvailable)
        {
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

                return new ErrorDataResult<bool>(false, "Randevu süresi dolmuş (yanıtlanmadı).");
            }

            return new SuccessDataResult<bool>(true);
        }


    }
}
