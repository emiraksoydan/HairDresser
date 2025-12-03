using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;


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
        IBarberStoreDal storeDal,IWorkingHourDal workingHourDal) : IAppointmentService
    {
        public async Task<IDataResult<bool>> AnyControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x => (x.FreeBarberUserId == id || x.CustomerUserId == id) && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyChairControl(Guid id)
        {

            var hasBlocking = await appointmentDal.AnyAsync(x => x.ChairId == id && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyStoreControl(Guid id)
        {
            var getBarberStore = await barberStoreDal.Get(x => x.Id == id);
            var hasStoreApp = await appointmentDal.AnyAsync(x => (x.BarberStoreUserId == getBarberStore.BarberStoreOwnerId) && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasStoreApp);
        }

        public async Task<IDataResult<List<ChairSlotDto>>> GetAvailibity(Guid storeId, DateOnly dateOnly, CancellationToken ct = default)
        {
            var res = await appointmentDal.GetAvailibilitySlot(storeId, dateOnly, ct);
            return new SuccessDataResult<List<ChairSlotDto>>(res);
        }

        public async Task<IDataResult<bool>> AnyManuelBarberControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x => (x.ManuelBarberId == id) && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasBlocking);
        }


        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateCustomerToStoreAndFreeBarberControlAsync(Guid customerUserId, CreateAppointmentRequestDto req)
        {
            var store = await storeDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>("Store not found");
            if (!req.ChairId.HasValue) return new ErrorDataResult<Guid>("ChairId is required");
            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>("StartTime/EndTime is required");

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>("Başlangıç saati bitişten büyük/eşit olamaz.");

            // geçmiş kontrolü
            var pastRes = EnsureNotPastAsync(req.AppointmentDate, start, bufferMinutes: 0);
            if (!pastRes.Success) return new ErrorDataResult<Guid>(pastRes.Message);

            // chair store'a ait mi?
            var chair = await chairDal.Get(c => c.Id == req.ChairId.Value && c.StoreId == req.StoreId);
            if (chair is null) return new ErrorDataResult<Guid>("Chair not found in store");

            // store açık mı?
            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // chair çakışma var mı?
            var overlapRes = await EnsureChairNoOverlapAsync(req.ChairId.Value, req.AppointmentDate, start, end);
            if (!overlapRes.Success) return new ErrorDataResult<Guid>(overlapRes.Message);

            // free barber dahilse sadece IsAvailable kontrolü
            if (req.FreeBarberUserId.HasValue)
            {
                var fbRes = await EnsureFreeBarberIsAvailableAsync(req.FreeBarberUserId.Value);
                if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);
            }

            // aktif kural (customer + freebarber tek aktif)
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
                FreeBarberUserId = req.FreeBarberUserId,

                RequestedBy = AppointmentRequester.Customer,
                Status = AppointmentStatus.Pending,
                StoreDecision = DecisionStatus.Pending,
                FreeBarberDecision = req.FreeBarberUserId.HasValue ? DecisionStatus.Pending : DecisionStatus.Approved,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(5),

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await appointmentDal.Add(appt);

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

            await CreateThreadIfNeeded(appt);
            return new SuccessDataResult<Guid>(appt.Id);
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req)
        {
            var store = await storeDal.Get(x => x.Id == req.StoreId);
            if (store is null) return new ErrorDataResult<Guid>("Store not found");
            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>("StartTime/EndTime is required");

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>("Başlangıç saati bitişten büyük/eşit olamaz.");

            var pastRes = EnsureNotPastAsync(req.AppointmentDate, start, bufferMinutes: 0);
            if (!pastRes.Success) return new ErrorDataResult<Guid>(pastRes.Message);

            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // Eğer free barber store’dan CHAIR seçerek alacaksa ChairId zorunlu yap.
            if (req.ChairId.HasValue)
            {
                var chair = await chairDal.Get(c => c.Id == req.ChairId.Value && c.StoreId == req.StoreId);
                if (chair is null) return new ErrorDataResult<Guid>("Koltuk dikkanda bulunamadı.");

                var overlapRes = await EnsureChairNoOverlapAsync(req.ChairId.Value, req.AppointmentDate, start, end);
                if (!overlapRes.Success) return new ErrorDataResult<Guid>(overlapRes.Message);
            }

            var rule = await EnforceActiveRules(customerId: null, freeBarberId: freeBarberUserId, storeOwnerId: store.BarberStoreOwnerId, AppointmentRequester.FreeBarber);
            if (!rule.Success) return new ErrorDataResult<Guid>(rule.Message);

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                ChairId = req.ChairId, // varsa set
                BarberStoreUserId = store.BarberStoreOwnerId,
                FreeBarberUserId = freeBarberUserId,

                AppointmentDate = req.AppointmentDate,
                StartTime = start,
                EndTime = end,

                RequestedBy = AppointmentRequester.FreeBarber,
                Status = AppointmentStatus.Pending,

                FreeBarberDecision = DecisionStatus.Approved,
                StoreDecision = DecisionStatus.Pending,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(5),

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await appointmentDal.Add(appt);
            await CreateThreadIfNeeded(appt);
            return new SuccessDataResult<Guid>(appt.Id);
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<Guid>> CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateAppointmentRequestDto req)
        {
            if (!req.FreeBarberUserId.HasValue) return new ErrorDataResult<Guid>("FreeBarberUserId is required");
            if (req.StartTime is null || req.EndTime is null) return new ErrorDataResult<Guid>("StartTime/EndTime is required");

            var start = (TimeSpan)req.StartTime!;
            var end = (TimeSpan)req.EndTime!;
            if (start >= end) return new ErrorDataResult<Guid>("Başlangıç saati bitişten büyük/eşit olamaz.");

            var store = await storeDal.Get(x => x.Id == req.StoreId && x.BarberStoreOwnerId == storeOwnerUserId);
            if (store is null) return new ErrorDataResult<Guid>("Store not found or not owner");

            var openRes = await EnsureStoreIsOpenAsync(req.StoreId, req.AppointmentDate, start, end);
            if (!openRes.Success) return new ErrorDataResult<Guid>(openRes.Message);

            // Freebarber sadece IsAvailable
            var fbRes = await EnsureFreeBarberIsAvailableAsync(req.FreeBarberUserId.Value);
            if (!fbRes.Success) return new ErrorDataResult<Guid>(fbRes.Message);

            var rule = await EnforceActiveRules(customerId: null, freeBarberId: req.FreeBarberUserId, storeOwnerId: storeOwnerUserId, AppointmentRequester.Store);
            if (!rule.Success) return new ErrorDataResult<Guid>(rule.Message);

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                BarberStoreUserId = storeOwnerUserId,
                FreeBarberUserId = req.FreeBarberUserId,
                AppointmentDate = req.AppointmentDate,
                StartTime = start,
                EndTime = end,

                RequestedBy = AppointmentRequester.Store,
                Status = AppointmentStatus.Pending,
                StoreDecision = DecisionStatus.Approved,
                FreeBarberDecision = DecisionStatus.Pending,
                PendingExpiresAt = DateTime.UtcNow.AddMinutes(5),

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await appointmentDal.Add(appt);
            await CreateThreadIfNeeded(appt);

            return new SuccessDataResult<Guid>(appt.Id);
        }


        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> StoreDecisionAsync(Guid storeOwnerUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, "Appointment not found");
            if (appt.BarberStoreUserId != storeOwnerUserId) return new ErrorDataResult<bool>(false, "Not authorized");
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>(false, "Not pending");

            appt.StoreDecision = approve ? DecisionStatus.Approved : DecisionStatus.Rejected;
            appt.UpdatedAt = DateTime.UtcNow;

            // Reject -> direkt biter
            if (!approve)
            {
                appt.Status = AppointmentStatus.Rejected;
                appt.PendingExpiresAt = null;
            }
            else
            {
                // diğer taraf onayladı mı?
                if (appt.FreeBarberDecision == DecisionStatus.Approved)
                {
                    appt.Status = AppointmentStatus.Approved;
                    appt.ApprovedAt = DateTime.UtcNow;
                    appt.PendingExpiresAt = null;
                }
            }

            await appointmentDal.Update(appt);
            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> FreeBarberDecisionAsync(Guid freeBarberUserId, Guid appointmentId, bool approve)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>("Appointment not found");
            if (appt.FreeBarberUserId != freeBarberUserId) return new ErrorDataResult<bool>("Not authorized");
            if (appt.Status != AppointmentStatus.Pending) return new ErrorDataResult<bool>("Not pending");

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
            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> CancelAsync(Guid userId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>(false, "Appointment not found");

            var isParticipant =
                appt.CustomerUserId == userId ||
                appt.FreeBarberUserId == userId ||
                appt.BarberStoreUserId == userId;

            if (!isParticipant) return new ErrorDataResult<bool>(false, "Not authorized");

            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<bool>(false, "Cannot cancel");

            appt.Status = AppointmentStatus.Cancelled;
            appt.CancelledByUserId = userId;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);



            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> CompleteAsync(Guid storeOwnerUserId, Guid appointmentId)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<bool>("Randevu bulunamadı");
            if (appt.BarberStoreUserId != storeOwnerUserId) return new ErrorDataResult<bool>("Yetki yok");
            if (appt.Status != AppointmentStatus.Approved) return new ErrorDataResult<bool>("Not approved");

            // zaman geçti mi? (basit): UTC yerine TR local istersen helper ekle
            var end = appt.AppointmentDate.ToDateTime(TimeOnly.FromTimeSpan((TimeSpan)appt.EndTime!));
            if (DateTime.UtcNow < DateTime.SpecifyKind(end, DateTimeKind.Utc))
                return new ErrorDataResult<bool>("Randevu süresi dolmadan tamamlanamaz");

            appt.Status = AppointmentStatus.Completed;
            appt.CompletedAt = DateTime.UtcNow;
            appt.UpdatedAt = DateTime.UtcNow;

            await appointmentDal.Update(appt);



            return new SuccessDataResult<bool>(true);
        }




        private static readonly AppointmentStatus[] Active = [AppointmentStatus.Pending, AppointmentStatus.Approved];

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

            // Store'un aynı anda 2 farklı freebarber çağırmaması (senin kuralın)
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

        private async Task CreateThreadIfNeeded(Appointment appt)
        {
            var exists = await threadDal.AnyAsync(t => t.AppointmentId == appt.Id);
            if (exists) return;

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
        }

        private async Task<IResult> EnsureChairNoOverlapAsync(Guid chairId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            // overlap kuralı: start < existing.End && end > existing.Start
            var hasOverlap = await appointmentDal.AnyAsync(x =>
                x.ChairId == chairId &&
                x.AppointmentDate == date &&
                (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved) &&
                x.StartTime < end &&
                x.EndTime > start
            );

            return hasOverlap
                ? new ErrorResult("Bu koltuk için seçilen saat aralığında başka bir randevu var.")
                : new SuccessResult();
        }

        private async Task<IResult> EnsureStoreIsOpenAsync(Guid storeId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            var dow = date.DayOfWeek;

            // O günün kaydı
            var wh = await workingHourDal.Get(x =>
                x.OwnerId == storeId &&
                x.DayOfWeek == dow
            // istersen: && x.IsActive
            );

            if (wh is null)
                return new ErrorResult("Dükkan bu gün için çalışma saati tanımlamamış (kapalı).");

            // Tatil/kapanış
            if (wh.IsClosed)
                return new ErrorResult("Dükkan bu gün kapalı (tatil).");

            // Saat aralığı store çalışma saatinin içinde mi?
            if (wh.StartTime > start || wh.EndTime < end)
                return new ErrorResult("Dükkan bu saat aralığında açık değil.");

            return new SuccessResult();
        }

        private async Task<IResult> EnsureFreeBarberIsAvailableAsync(Guid freeBarberUserId)
        {
            var fb = await freeBarberDal.Get(u => u.Id == freeBarberUserId); 
            if (fb is null) return new ErrorResult("Serbest berber bulunamadı.");

            if (!fb.IsAvailable)
                return new ErrorResult("Serbest berber şu an müsait değil.");

            return new SuccessResult();
        }

        private IResult EnsureNotPastAsync(DateOnly date, TimeSpan start, int bufferMinutes = 0)
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
    }
}
