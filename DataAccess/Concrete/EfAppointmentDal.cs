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
                    && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved)
                    && a.StartTime.HasValue && a.EndTime.HasValue) // Nullable kontrolü
                .Select(a => new { ChairId = a.ChairId!.Value, StartTime = a.StartTime!.Value, EndTime = a.EndTime!.Value })
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
            // 1. ADIM: Randevuları Çek
            // ---------------------------------------------------------------------------
            var query = _context.Appointments.AsNoTracking()
                .Where(x => (x.CustomerUserId == currentUserId && !x.IsDeletedByCustomerUserId) ||
                            (x.BarberStoreUserId == currentUserId && !x.IsDeletedByBarberStoreUserId) ||
                            (x.FreeBarberUserId == currentUserId && !x.IsDeletedByFreeBarberUserId));

            // Filtreleme
            switch (appointmentFilter)
            {
                case AppointmentFilter.Active:
                    // Active tab'da sadece Approved randevular görünmeli (Pending'ler gözükmeyecek)
                    query = query.Where(x => x.Status == AppointmentStatus.Approved);
                    break;
                case AppointmentFilter.Completed:
                    query = query.Where(x => x.Status == AppointmentStatus.Completed);
                    break;
                case AppointmentFilter.Cancelled:
                    query = query.Where(x => x.Status == AppointmentStatus.Cancelled || x.Status == AppointmentStatus.Rejected || x.Status == AppointmentStatus.Unanswered);
                    break;
                case AppointmentFilter.Pending:
                    query = query.Where(x => x.Status == AppointmentStatus.Pending);
                    break;
            }

            var appointments = await query.ToListAsync();

            if (appointments.Count == 0) return new List<AppointmentGetDto>();

            // ---------------------------------------------------------------------------
            // 2. ADIM: ID Toplama
            // ---------------------------------------------------------------------------
            var appointmentIds = appointments.Select(x => x.Id).Distinct().ToList();

            // User ID'ler
            var storeUserIds = appointments.Where(x => x.BarberStoreUserId.HasValue).Select(x => x.BarberStoreUserId.Value).Distinct().ToList();
            var freeBarberUserIds = appointments.Where(x => x.FreeBarberUserId.HasValue).Select(x => x.FreeBarberUserId.Value).Distinct().ToList();
            var customerIds = appointments.Where(x => x.CustomerUserId.HasValue).Select(x => x.CustomerUserId.Value).Distinct().ToList();

            // Gerçek ID'ler
            var manuelBarberIds = appointments.Where(x => x.ManuelBarberId.HasValue).Select(x => x.ManuelBarberId.Value).Distinct().ToList();

            // ---------------------------------------------------------------------------
            // 3. ADIM: Veri Çekme (Batch Queries)
            // ---------------------------------------------------------------------------

            // A) STORE: StoreId, Name, Pricing, TYPE
            // FIX: GroupBy kullanarak duplicate key hatasını önle (aynı ownerId'ye sahip birden fazla store olabilir)
            // EF Core'da GroupBy().Select(g => g.First()) projection hatası veriyor, bu yüzden memory'de GroupBy yapıyoruz
            var allStores = await _context.BarberStores.AsNoTracking()
                .Where(s => storeUserIds.Contains(s.BarberStoreOwnerId))
                .Select(s => new
                {
                    s.BarberStoreOwnerId,
                    RealStoreId = s.Id,
                    s.StoreName,
                    s.PricingType,
                    s.PricingValue,
                    s.Type,
                    s.AddressDescription
                })
                .ToListAsync();
            
            // Memory'de GroupBy yap (aynı ownerId'ye sahip birden fazla store varsa ilkini al)
            var storesList = allStores
                .GroupBy(s => s.BarberStoreOwnerId)
                .Select(g => g.First())
                .ToList();
            
            var storesDict = storesList.ToDictionary(s => s.BarberStoreOwnerId, s => new
            {
                s.RealStoreId,
                s.StoreName,
                s.PricingType,
                s.PricingValue,
                s.Type,
                s.AddressDescription
            });

            // B) FREE BARBER
            // Önce list olarak çek, sonra GroupBy ile duplicate key'leri handle et
            var freeBarbersList = await _context.FreeBarbers.AsNoTracking()
                .Where(fb => freeBarberUserIds.Contains(fb.FreeBarberUserId))
                .Select(fb => new
                {
                    RealFreeBarberId = fb.Id,
                    FreeBarberUserId = fb.FreeBarberUserId, // Favori kontrolü için User ID
                    FullName = fb.FirstName + " " + fb.LastName,
                    CreatedAt = fb.CreatedAt
                })
                .ToListAsync();
            
            var freeBarberDict = freeBarbersList
                .GroupBy(fb => fb.FreeBarberUserId)
                .ToDictionary(g => g.Key, g => new
                {
                    g.First().RealFreeBarberId,
                    g.First().FreeBarberUserId,
                    g.First().FullName
                });

            // C) MANUEL BARBER
            var manuelBarberDict = await _context.ManuelBarbers.AsNoTracking()
                .Where(m => manuelBarberIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.FullName);

            // D) CUSTOMER
            var customerList = await _context.Users.AsNoTracking()
                .Where(u => customerIds.Contains(u.Id))
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, u.CustomerNumber })
                .ToListAsync();
            var customerDict = customerList.ToDictionary(u => u.Id, u => u.Name);
            var customerNumberDict = customerList.ToDictionary(u => u.Id, u => u.CustomerNumber);

            // D-2) STORE OWNER & FREE BARBER CustomerNumber'ları
            var storeOwnerAndFreeBarberUserIds = storeUserIds.Concat(freeBarberUserIds).Distinct().ToList();
            var storeOwnerAndFreeBarberNumberList = await _context.Users.AsNoTracking()
                .Where(u => storeOwnerAndFreeBarberUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.CustomerNumber })
                .ToListAsync();
            var storeOwnerAndFreeBarberNumberDict = storeOwnerAndFreeBarberNumberList.ToDictionary(u => u.Id, u => u.CustomerNumber);

            // ---------------------------------------------------------------------------
            // 4. ADIM: Yan Veri ID'lerini Hazırla (Resim & Favori)
            // ---------------------------------------------------------------------------
            var realStoreIds = storesDict.Values.Select(x => x.RealStoreId).ToList();
            var realFreeBarberIds = freeBarberDict.Values.Select(x => x.RealFreeBarberId).ToList();

            // Resim ID'leri
            var allIdsForImages = new List<Guid>();
            allIdsForImages.AddRange(realStoreIds);
            allIdsForImages.AddRange(realFreeBarberIds);
            allIdsForImages.AddRange(manuelBarberIds);
            allIdsForImages.AddRange(customerIds);
            allIdsForImages = allIdsForImages.Distinct().ToList();

            // Favori ID'leri
            // Store için: Store ID
            // FreeBarber için: FreeBarber User ID (FavoritedToId = FreeBarberUserId)
            // Customer için: Customer User ID
            var allIdsForFav = new List<Guid>();
            allIdsForFav.AddRange(realStoreIds);
            // FreeBarber için User ID'leri ekle (FavoritedToId = FreeBarberUserId)
            var freeBarberUserIdsForFav = freeBarberDict.Values.Select(x => x.FreeBarberUserId).ToList();
            allIdsForFav.AddRange(freeBarberUserIdsForFav);
            allIdsForFav.AddRange(customerIds);
            allIdsForFav = allIdsForFav.Distinct().ToList();

            // ---------------------------------------------------------------------------
            // 5. ADIM: Yan Verileri Çek
            // ---------------------------------------------------------------------------

            // Resimler - ImageOwnerType kontrolü ile
            var imagesList = await _context.Images.AsNoTracking()
                .Where(i => 
                    (i.OwnerType == ImageOwnerType.Store && realStoreIds.Contains(i.ImageOwnerId)) ||
                    (i.OwnerType == ImageOwnerType.FreeBarber && realFreeBarberIds.Contains(i.ImageOwnerId)) ||
                    (i.OwnerType == ImageOwnerType.ManuelBarber && manuelBarberIds.Contains(i.ImageOwnerId)) ||
                    (i.OwnerType == ImageOwnerType.User && customerIds.Contains(i.ImageOwnerId))
                )
                .Select(i => new { i.ImageOwnerId, i.ImageUrl })
                .ToListAsync();

            var imagesDict = imagesList
                .GroupBy(x => x.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.ImageUrl ?? "");

            // Favoriler
            var myFavorites = await _context.Favorites.AsNoTracking()
                .Where(f => f.FavoritedFromId == currentUserId && allIdsForFav.Contains(f.FavoritedToId) && f.IsActive)
                .Select(f => f.FavoritedToId)
                .ToListAsync();

            var favSet = new HashSet<Guid>(myFavorites);

            // Rating - Kullanıcının yaptığı rating'ler
            var myRatings = await _context.Ratings.AsNoTracking()
                .Where(r => r.RatedFromId == currentUserId && appointmentIds.Contains(r.AppointmentId))
                .Select(r => new { r.AppointmentId, r.TargetId, r.Score, r.Comment })
                .ToListAsync();

            var ratingDict = myRatings.ToDictionary(r => (r.AppointmentId, r.TargetId), r => r);

            // Ortalama Rating'ler - Store, FreeBarber, ManuelBarber, Customer için
            // Store için: Store ID
            // FreeBarber için: FreeBarber User ID (TargetId = FreeBarberUserId)
            // Customer için: Customer User ID
            var allTargetIds = new List<Guid>();
            allTargetIds.AddRange(realStoreIds);
            // FreeBarber için User ID'leri ekle (TargetId = FreeBarberUserId)
            allTargetIds.AddRange(freeBarberUserIdsForFav);
            allTargetIds.AddRange(manuelBarberIds);
            allTargetIds.AddRange(customerIds);
            allTargetIds = allTargetIds.Distinct().ToList();

            var averageRatings = await _context.Ratings.AsNoTracking()
                .Where(r => allTargetIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new { TargetId = g.Key, AverageRating = g.Average(x => (double)x.Score) })
                .ToListAsync();

            var averageRatingDict = averageRatings.ToDictionary(x => x.TargetId, x => x.AverageRating);

            // Hizmetler (Services) - AppointmentServiceOffering'ler
            var appointmentServices = await _context.AppointmentServiceOfferings.AsNoTracking()
                .Where(aso => appointmentIds.Contains(aso.AppointmentId))
                .Select(aso => new { aso.AppointmentId, aso.ServiceOfferingId, aso.ServiceName, aso.Price })
                .ToListAsync();

            var servicesDict = appointmentServices
                .GroupBy(aso => aso.AppointmentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(s => new AppointmentServiceDto
                    {
                        ServiceId = s.ServiceOfferingId,
                        ServiceName = s.ServiceName,
                        Price = s.Price
                    }).ToList()
                );

            // ---------------------------------------------------------------------------
            // 6. ADIM: Mapping
            // ---------------------------------------------------------------------------
            var resultList = new List<AppointmentGetDto>();

            foreach (var appt in appointments)
            {
                var dto = new AppointmentGetDto
                {
                    Id = appt.Id,
                    Status = appt.Status,
                    AppointmentDate = appt.AppointmentDate, // Nullable
                    StartTime = appt.StartTime, // Nullable
                    EndTime = appt.EndTime, // Nullable
                    CreatedAt = appt.CreatedAt,
                    ChairId = appt.ChairId,
                    AppointmentRequester = appt.RequestedBy,
                    StoreDecision = appt.StoreDecision,
                    FreeBarberDecision = appt.FreeBarberDecision,
                    CustomerDecision = appt.CustomerDecision,
                    StoreSelectionType = appt.StoreSelectionType,
                    Note = appt.Note,
                };

                // Hizmetler (Services) ve Toplam Fiyat
                if (servicesDict.TryGetValue(appt.Id, out var services))
                {
                    dto.Services = services;
                    dto.TotalPrice = services.Sum(s => s.Price);
                }
                else
                {
                    dto.Services = new List<AppointmentServiceDto>();
                    dto.TotalPrice = 0;
                }

                // Koltuk adı: Entity'den snapshot olarak alınıyor (koltuk silinse bile korunur)
                dto.ChairName = appt.ChairName;

                // --- STORE ---
                if (appt.BarberStoreUserId.HasValue)
                {
                    var userId = appt.BarberStoreUserId.Value;
                    dto.StoreUserId = userId; // Şikayet için Store sahibinin User ID'si

                    if (storesDict.TryGetValue(userId, out var sInfo))
                    {
                        var realStoreId = sInfo.RealStoreId;

                        dto.BarberStoreId = realStoreId;
                        dto.StoreName = sInfo.StoreName;
                        dto.PricingType = sInfo.PricingType;
                        dto.PricingValue = sInfo.PricingValue;
                        dto.StoreType = sInfo.Type; // Store Type
                        dto.StoreAddressDescription = sInfo.AddressDescription; // Store Address Description

                        if (imagesDict.TryGetValue(realStoreId, out var img)) dto.StoreImage = img;
                        dto.IsStoreFavorite = favSet.Contains(realStoreId);

                        if (ratingDict.TryGetValue((appt.Id, realStoreId), out var r))
                        {
                            dto.MyRatingForStore = r.Score;
                            dto.MyCommentForStore = r.Comment;
                        }

                        // Store'un ortalama rating'i
                        if (averageRatingDict.TryGetValue(realStoreId, out var avgRating))
                        {
                            dto.StoreAverageRating = avgRating;
                        }

                        // Store owner'ın customerNumber'ı
                        if (storeOwnerAndFreeBarberNumberDict.TryGetValue(userId, out var storeOwnerNumber))
                        {
                            dto.StoreOwnerNumber = storeOwnerNumber;
                        }
                    }
                }

                // --- FREE BARBER ---
                if (appt.FreeBarberUserId.HasValue)
                {
                    var userId = appt.FreeBarberUserId.Value;
                    dto.FreeBarberUserId = userId; // Şikayet için FreeBarber'ın User ID'si

                    if (freeBarberDict.TryGetValue(userId, out var fbInfo))
                    {
                        var realFbId = fbInfo.RealFreeBarberId;

                        dto.FreeBarberId = realFbId;
                        dto.FreeBarberName = fbInfo.FullName;

                        if (imagesDict.TryGetValue(realFbId, out var img)) dto.FreeBarberImage = img;
                        // Favori kontrolü: FavoritedToId = FreeBarber User ID
                        dto.IsFreeBarberFavorite = favSet.Contains(fbInfo.FreeBarberUserId);

                        // Rating kontrolü: TargetId = FreeBarber User ID
                        if (ratingDict.TryGetValue((appt.Id, fbInfo.FreeBarberUserId), out var r))
                        {
                            dto.MyRatingForFreeBarber = r.Score;
                            dto.MyCommentForFreeBarber = r.Comment;
                        }

                        // FreeBarber'ın ortalama rating'i: TargetId = FreeBarber User ID
                        if (averageRatingDict.TryGetValue(fbInfo.FreeBarberUserId, out var avgRating))
                        {
                            dto.FreeBarberAverageRating = avgRating;
                        }

                        // FreeBarber'ın customerNumber'ı
                        if (storeOwnerAndFreeBarberNumberDict.TryGetValue(userId, out var freeBarberNumber))
                        {
                            dto.FreeBarberNumber = freeBarberNumber;
                        }
                    }
                }

                // --- MANUEL BARBER ---
                if (appt.ManuelBarberId.HasValue)
                {
                    var mbId = appt.ManuelBarberId.Value;
                    dto.ManuelBarberId = mbId;

                    if (manuelBarberDict.TryGetValue(mbId, out var mbName)) dto.ManuelBarberName = mbName;
                    if (imagesDict.TryGetValue(mbId, out var img)) dto.ManuelBarberImage = img;

                    if (ratingDict.TryGetValue((appt.Id, mbId), out var r))
                    {
                        dto.MyRatingForManuelBarber = r.Score;
                        dto.MyCommentForManuelBarber = r.Comment;
                    }

                    // ManuelBarber'ın ortalama rating'i
                    if (averageRatingDict.TryGetValue(mbId, out var avgRating))
                    {
                        dto.ManuelBarberAverageRating = avgRating;
                    }
                }

                // --- CUSTOMER ---
                if (appt.CustomerUserId.HasValue && appt.CustomerUserId != currentUserId)
                {
                    var cId = appt.CustomerUserId.Value;
                    dto.CustomerUserId = cId;

                    // Düzeltildi: cName -> customerNameVal
                    if (customerDict.TryGetValue(cId, out var customerNameVal)) dto.CustomerName = customerNameVal;
                    if (customerNumberDict.TryGetValue(cId, out var customerNumberVal)) dto.CustomerNumber = customerNumberVal;

                    if (imagesDict.TryGetValue(cId, out var img)) dto.CustomerImage = img;

                    dto.IsCustomerFavorite = favSet.Contains(cId);

                    if (ratingDict.TryGetValue((appt.Id, cId), out var r))
                    {
                        dto.MyRatingForCustomer = r.Score;
                        dto.MyCommentForCustomer = r.Comment;
                    }

                    // Customer'ın ortalama rating'i
                    if (averageRatingDict.TryGetValue(cId, out var avgRating))
                    {
                        dto.CustomerAverageRating = avgRating;
                    }
                }

                resultList.Add(dto);
            }

            return resultList;
        }
    }
}
