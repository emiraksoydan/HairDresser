using Core.DataAccess.EntityFramework;
using Core.Utilities.Configuration;
using Core.Utilities.Helpers;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


namespace DataAccess.Concrete
{
    public class EfAppointmentDal : EfEntityRepositoryBase<Appointment, DatabaseContext>, IAppointmentDal
    {
        private readonly DatabaseContext _context;
        private readonly AppointmentSettings _settings;
        
        public EfAppointmentDal(DatabaseContext context, IOptions<AppointmentSettings> appointmentSettings) : base(context)
        {
            _context = context;
            _settings = appointmentSettings.Value;
        }

        public async Task<List<ChairSlotDto>> GetAvailibilitySlot(Guid storeId, DateOnly dateOnly, CancellationToken ct)
        {
            var slotMinutes = _settings.SlotMinutes;

            // 1) Koltuklar
            var chairs = await _context.BarberChairs.AsNoTracking()
                .Where(c => c.StoreId == storeId)
                .Select(c => new
                {
                    c.Id,
                    ChairName = c.Name,
                    c.ManuelBarberId
                })
                .ToListAsync(ct);

            if (chairs.Count == 0)
                return new List<ChairSlotDto>();

            var chairIds = chairs.Select(x => x.Id).ToList();

            // 2) O günün aktif randevuları (chair bazlı)
            var appts = await _context.Appointments.AsNoTracking()
                .Where(a => a.ChairId != null
                    && chairIds.Contains(a.ChairId.Value)
                    && a.AppointmentDate == dateOnly
                    && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved))
                .Select(a => new { ChairId = a.ChairId!.Value, a.StartTime, a.EndTime })
                .ToListAsync(ct);

            var apptMap = appts
                .GroupBy(x => x.ChairId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (x.StartTime, x.EndTime)).ToList()
                );

            // 3) WorkingHour (store)
            var wh = await _context.WorkingHours.AsNoTracking()
                .Where(w => w.OwnerId == storeId
                    && w.DayOfWeek == dateOnly.DayOfWeek
                    && w.IsClosed == false)
                .Select(w => new { w.StartTime, w.EndTime })
                .FirstOrDefaultAsync(ct);

            var slotRanges = wh is null
                ? new List<(TimeSpan start, TimeSpan end)>()
                : BuildSlots(wh.StartTime, wh.EndTime, slotMinutes);

            // 4) TR zamanı (IsPast)
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var today = DateOnly.FromDateTime(nowLocal);

            // 5) Manuel berber isimleri
            var manualIds = chairs
                .Where(x => x.ManuelBarberId != null)
                .Select(x => x.ManuelBarberId!.Value)
                .Distinct()
                .ToList();

            var manualMap = manualIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.ManuelBarbers.AsNoTracking()
                    .Where(m => manualIds.Contains(m.Id))
                    .Select(m => new { m.Id, m.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName,ct);

            // 6) Manuel berber rating ortalaması
            var ratingRows = await _context.Ratings.AsNoTracking()
                .Where(r => manualIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new { TargetId = g.Key, Avg = g.Average(x => x.Score), Count = g.Count() })
                .ToListAsync(ct);

            var ratingMap = ratingRows.ToDictionary(x => x.TargetId, x => (x.Avg, x.Count));

            // 7) Response
            var result = new List<ChairSlotDto>(chairs.Count);

            foreach (var c in chairs)
            {
                Guid? barberId = null;
                string? barberName = null;
                double? barberRating = null;

                if (c.ManuelBarberId != null)
                {
                    barberId = c.ManuelBarberId.Value;
                    manualMap.TryGetValue(barberId.Value, out barberName);

                    if (ratingMap.TryGetValue(barberId.Value, out var r))
                        barberRating = r.Avg;
                }
                apptMap.TryGetValue(c.Id, out var chairAppts);
                chairAppts ??= new List<(TimeSpan StartTime, TimeSpan EndTime)>();

                var slots = slotRanges.Select(s =>
                {
                    var booked = chairAppts.Any(a => a.StartTime < s.end && a.EndTime > s.start);

                    var isPast =
                        dateOnly < today ? true :
                        dateOnly > today ? false :
                        s.start <= nowLocal.TimeOfDay;

                    return new SlotDto
                    {
                        SlotId = StableSlotId(c.Id, dateOnly, s.start, s.end),
                        Start = ToHHmm(s.start),
                        End = ToHHmm(s.end),
                        IsBooked = booked,
                        IsPast = isPast
                    };
                }).ToList();

                result.Add(new ChairSlotDto
                {
                    ChairId = c.Id,
                    ChairName = c.ChairName,
                    BarberId = barberId,
                    BarberName = barberName,
                    BarberRating = barberRating,
                    Slots = slots
                });
            }

            return result;
        }


        static List<(TimeSpan start, TimeSpan end)> BuildSlots(TimeSpan start, TimeSpan end, int slotMin)
        {
            var list = new List<(TimeSpan, TimeSpan)>();
            for (var t = start; t + TimeSpan.FromMinutes(slotMin) <= end; t += TimeSpan.FromMinutes(slotMin))
                list.Add((t, t + TimeSpan.FromMinutes(slotMin)));
            return list;
        }

        static string ToHHmm(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";

        static Guid StableSlotId(Guid chairId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            var raw = $"{chairId:N}|{date:yyyyMMdd}|{(int)start.TotalMinutes}|{(int)end.TotalMinutes}";
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            return new Guid(bytes);
        }

        public async Task<List<AppointmentGetDto>> GetAllAppointmentByFilter(Guid currentUserId, AppointmentFilter appointmentFilter)
        {
            // ---------------------------------------------------------------------------
            // 1. ADIM: Temel Sorgu (Kullanıcıya ait kayıtlar)
            // ---------------------------------------------------------------------------
            var query = _context.Appointments.AsNoTracking()
                .Where(x => x.CustomerUserId == currentUserId ||
                            x.BarberStoreUserId == currentUserId ||
                            x.FreeBarberUserId == currentUserId);

            // ---------------------------------------------------------------------------
            // 2. ADIM: Enum Filtreleme (DÜZELTİLEN KISIM)
            // ---------------------------------------------------------------------------
            switch (appointmentFilter)
            {
                case AppointmentFilter.Active:
                    // Aktif = Bekleyen (Pending) veya Onaylanan (Approved)
                    query = query.Where(x => x.Status == AppointmentStatus.Pending ||
                                             x.Status == AppointmentStatus.Approved);
                    break;

                case AppointmentFilter.Completed:
                    // Tamamlanan = Completed
                    query = query.Where(x => x.Status == AppointmentStatus.Completed);
                    break;

                case AppointmentFilter.Cancelled:
                    // İptal/Geçmiş = İptal (Cancelled), Red (Rejected) veya Cevapsız (Unanswered)
                    query = query.Where(x => x.Status == AppointmentStatus.Cancelled ||
                                             x.Status == AppointmentStatus.Rejected ||
                                             x.Status == AppointmentStatus.Unanswered);
                    break;
            }

            var appointments = await query.ToListAsync();

            if (appointments.Count == 0)
            {
                return new List<AppointmentGetDto>();
            }

            // ---------------------------------------------------------------------------
            // 3. ADIM: ID Toplama (Batch Query Hazırlığı)
            // ---------------------------------------------------------------------------
            var appointmentIds = appointments.Select(x => x.Id).Distinct().ToList();

            var storeOwnerIds = appointments.Where(x => x.BarberStoreUserId.HasValue).Select(x => x.BarberStoreUserId.Value).Distinct().ToList();
            var freeBarberUserIds = appointments.Where(x => x.FreeBarberUserId.HasValue).Select(x => x.FreeBarberUserId.Value).Distinct().ToList();
            var customerIds = appointments.Where(x => x.CustomerUserId.HasValue).Select(x => x.CustomerUserId.Value).Distinct().ToList();
            var manuelBarberIds = appointments.Where(x => x.ManuelBarberId.HasValue).Select(x => x.ManuelBarberId.Value).Distinct().ToList();

            // Resim için tüm ID'ler
            var allIdsForImages = storeOwnerIds
                .Concat(freeBarberUserIds)
                .Concat(customerIds)
                .Concat(manuelBarberIds)
                .Distinct()
                .ToList();

            // Favori için ID'ler (Manuel Berber HARİÇ)
            var allTargetIdsForFav = storeOwnerIds
                .Concat(freeBarberUserIds)
                .Concat(customerIds)
                .Distinct()
                .ToList();

            // ---------------------------------------------------------------------------
            // 4. ADIM: Veri Çekme (Toplu Sorgular)
            // ---------------------------------------------------------------------------

            // A) İsimler
            var storesDict = await _context.BarberStores.AsNoTracking()
                .Where(s => storeOwnerIds.Contains(s.BarberStoreOwnerId))
                .ToDictionaryAsync(s => s.BarberStoreOwnerId, s => s.StoreName);

            var freeBarberDict = await _context.FreeBarbers.AsNoTracking()
                .Where(fb => freeBarberUserIds.Contains(fb.FreeBarberUserId))
                .ToDictionaryAsync(fb => fb.FreeBarberUserId, fb => fb.FirstName + " " + fb.LastName);

            var manuelBarberDict = await _context.ManuelBarbers.AsNoTracking()
                .Where(m => manuelBarberIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.FullName);

            var customerDict = await _context.Users.AsNoTracking()
                .Where(u => customerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName);

            // B) Resimler
            var imagesList = await _context.Images.AsNoTracking()
                .Where(i => allIdsForImages.Contains(i.ImageOwnerId))
                .Select(i => new { i.ImageOwnerId, i.ImageUrl })
                .ToListAsync();

            var imagesDict = imagesList
                .GroupBy(x => x.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.ImageUrl ?? "");

            // C) Favoriler
            var myFavorites = await _context.Favorites.AsNoTracking()
                .Where(f => f.FavoritedFromId == currentUserId && allTargetIdsForFav.Contains(f.FavoritedToId))
                .Select(f => f.FavoritedToId)
                .ToListAsync();

            var favSet = new HashSet<Guid>(myFavorites);

            // D) Rating & Yorumlar
            var myRatings = await _context.Ratings.AsNoTracking()
                .Where(r => r.RatedFromId == currentUserId && appointmentIds.Contains(r.AppointmentId))
                .Select(r => new { r.AppointmentId, r.TargetId, r.Score, r.Comment })
                .ToListAsync();

            var ratingDict = myRatings.ToDictionary(r => (r.AppointmentId, r.TargetId), r => r);

            // ---------------------------------------------------------------------------
            // 5. ADIM: Mapping
            // ---------------------------------------------------------------------------
            var resultList = new List<AppointmentGetDto>();

            foreach (var appt in appointments)
            {
                var dto = new AppointmentGetDto
                {
                    Id = appt.Id,
                    Status = appt.Status,
                    AppointmentDate = appt.AppointmentDate,
                    StartTime = appt.StartTime,
                    EndTime = appt.EndTime,
                    CreatedAt = appt.CreatedAt,
                    ChairId = appt.ChairId
                };

                // STORE
                if (appt.BarberStoreUserId.HasValue)
                {
                    var sId = appt.BarberStoreUserId.Value;
                    dto.BarberStoreId = sId;
                    if (storesDict.TryGetValue(sId, out var sName)) dto.StoreName = sName;
                    if (imagesDict.TryGetValue(sId, out var sImg)) dto.StoreImage = sImg;

                    dto.IsStoreFavorite = favSet.Contains(sId);

                    if (ratingDict.TryGetValue((appt.Id, sId), out var r))
                    {
                        dto.MyRatingForStore = r.Score;
                        dto.MyCommentForStore = r.Comment;
                    }
                }

                // FREE BARBER
                if (appt.FreeBarberUserId.HasValue)
                {
                    var fbId = appt.FreeBarberUserId.Value;
                    dto.FreeBarberId = fbId;
                    if (freeBarberDict.TryGetValue(fbId, out var fbName)) dto.FreeBarberName = fbName;
                    if (imagesDict.TryGetValue(fbId, out var fbImg)) dto.FreeBarberImage = fbImg;

                    dto.IsFreeBarberFavorite = favSet.Contains(fbId);

                    if (ratingDict.TryGetValue((appt.Id, fbId), out var r))
                    {
                        dto.MyRatingForFreeBarber = r.Score;
                        dto.MyCommentForFreeBarber = r.Comment;
                    }
                }

                // MANUEL BARBER
                if (appt.ManuelBarberId.HasValue)
                {
                    var mbId = appt.ManuelBarberId.Value;
                    dto.ManuelBarberId = mbId;
                    if (manuelBarberDict.TryGetValue(mbId, out var mbName)) dto.ManuelBarberName = mbName;

                    // Manuel Barber Resmi (Varsa)
                    if (imagesDict.TryGetValue(mbId, out var mbImg)) dto.ManuelBarberImage = mbImg;

                    if (ratingDict.TryGetValue((appt.Id, mbId), out var r))
                    {
                        dto.MyRatingForManuelBarber = r.Score;
                        dto.MyCommentForManuelBarber = r.Comment;
                    }
                }

                // CUSTOMER
                if (appt.CustomerUserId.HasValue && appt.CustomerUserId != currentUserId)
                {
                    var cId = appt.CustomerUserId.Value;
                    dto.CustomerUserId = cId;
                    if (customerDict.TryGetValue(cId, out var cName)) dto.CustomerName = cName;
                    if (imagesDict.TryGetValue(cId, out var cImg)) dto.CustomerImage = cImg;

                    dto.IsCustomerFavorite = favSet.Contains(cId);

                    if (ratingDict.TryGetValue((appt.Id, cId), out var r))
                    {
                        dto.MyRatingForCustomer = r.Score;
                        dto.MyCommentForCustomer = r.Comment;
                    }
                }

                resultList.Add(dto);
            }

            return resultList;
        }
    }
}
