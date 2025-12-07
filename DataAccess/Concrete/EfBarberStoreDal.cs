using Core.DataAccess.EntityFramework;
using Core.Utilities.Helpers;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfBarberStoreDal : EfEntityRepositoryBase<BarberStore, DatabaseContext>, IBarberStoreDal
    {
        private readonly DatabaseContext _context;
        public EfBarberStoreDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<BarberStoreMineDto> GetBarberStoreForUsers(Guid storeId)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            // 1) Store
            var store = await _context.BarberStores
                .AsNoTracking()
                .Where(s => s.Id == storeId)
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.Type,
                    s.AddressDescription,
                    s.PricingValue,
                    s.PricingType,
                })
                .FirstOrDefaultAsync();

            if (store == null)
                return new BarberStoreMineDto();

            // 2) Rating + review count (tek store)
            var ratingInfo = await _context.Ratings
                .AsNoTracking()
                .Where(r => r.TargetId == store.Id)
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .FirstOrDefaultAsync();

            // 3) Favorite count
            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == store.Id);

            // 4) Offerings
            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == store.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            // 5) Working hours
            var hours = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => w.OwnerId == store.Id)
                .ToListAsync();

            // 6) Images
            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store && i.ImageOwnerId == store.Id)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var isOpenNow = OpenControl.IsOpenNow(hours, nowLocal);

            return new BarberStoreMineDto
            {
                Id = store.Id,
                StoreName = store.StoreName,
                ImageList = images,
                Type = store.Type,
                Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                ReviewCount = ratingInfo?.ReviewCount ?? 0,
                FavoriteCount = favoriteCount,
                IsOpenNow = isOpenNow,
                ServiceOfferings = offerings,
                AddressDescription = store.AddressDescription,
                PricingType = store.PricingType.ToString(),
                PricingValue = store.PricingValue,
            };
        }


        public async Task<BarberStoreDetail> GetByIdStore(Guid storeId)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }

            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            var today = nowLocal.DayOfWeek;   // 0–6
            var nowTime = nowLocal.TimeOfDay;      // TimeSpan

            // 2) Store'u çek
            var store = await _context.BarberStores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == storeId);

            if (store is null)
                return new BarberStoreDetail();

            // 3) Store'a ait çalışma saatlerini çek
            var hours = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => w.OwnerId == storeId /* && w.TargetType == ... (varsa) */)
                .OrderBy(w => w.DayOfWeek)
                .ThenBy(w => w.StartTime)
                .ToListAsync();

            // 4) Şu an açık mı?
            var isOpenNow = hours.Any(h =>
                !h.IsClosed &&
                h.DayOfWeek == today &&
                h.StartTime <= nowTime &&
                nowTime < h.EndTime
            );

            // 5) WorkingHours'u DTO'ya map et
            var workingHourDtos = hours.Select(h => new WorkingHourDto
            {
                Id = h.Id,
                OwnerId = h.OwnerId,
                DayOfWeek = h.DayOfWeek,
                IsClosed = h.IsClosed,
                StartTime = h.StartTime,
                EndTime = h.EndTime
            }).ToList();

            var images = await _context.Images.AsNoTracking().Where(i => i.ImageOwnerId == storeId && i.OwnerType == ImageOwnerType.Store).ToListAsync();

            var imageDtos = images.Select(i => new ImageGetDto
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
            }).ToList();

            var serviceOfferings = await _context.ServiceOfferings.AsNoTracking().Where(i => i.OwnerId == storeId).ToListAsync();
            var serviceOfferingsDto = serviceOfferings.Select(s => new ServiceOfferingGetDto
            {
                Id = s.Id,
                Price = s.Price,
                ServiceName = s.ServiceName
            }).ToList();

            var manuelBarberDtos = await _context.ManuelBarbers
          .AsNoTracking()
          .Where(b => b.StoreId == storeId)
          .Select(b => new ManuelBarberDto
          {
              Id = b.Id,
              FullName = b.FullName,
              Rating = _context.Ratings
                  .Where(r => r.TargetId == b.Id)
                  .Select(r => (double?)r.Score)      // nullable’a çevir
                  .Average() ?? 0,                    // null ise 0
              ProfileImageUrl = _context.Images
                  .Where(i => i.ImageOwnerId == b.Id)
                  .Select(i => i.ImageUrl)
                  .FirstOrDefault()
          })
          .ToListAsync();


            var chairs = await _context.BarberChairs
             .AsNoTracking()
             .Where(ch => ch.StoreId == storeId)
             .ToListAsync();

            var barberStoreChairsDto = chairs
          .Select(ch => new BarberChairDto
          {
              Id = ch.Id,
              ManualBarberId = ch.ManuelBarberId, // null olabilir
              Name = ch.Name,
          })
          .ToList();

            // 6) BarberStoreDetail DTO'sunu doldur
            var dto = new BarberStoreDetail
            {
                Id = store.Id,
                StoreName = store.StoreName,
                Latitude = store.Latitude,
                Longitude = store.Longitude,
                Type = store.Type.ToString(),
                PricingType = store.PricingType.ToString(),
                PricingValue = store.PricingValue,
                IsOpenNow = isOpenNow,
                WorkingHours = workingHourDtos,
                AddressDescription = store.AddressDescription,
                ImageList = imageDtos,
                ServiceOfferings = serviceOfferingsDto,
                ManuelBarbers = manuelBarberDtos,
                BarberStoreChairs = barberStoreChairsDto,
                TaxDocumentFilePath = store.TaxDocumentFilePath,

            };
            return dto;
        }

        public async Task<List<BarberStoreMineDto>> GetMineStores(Guid currentUserId)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            // 1) Bu kullanıcıya ait dükkanları çek
            var stores = await _context.BarberStores
                .AsNoTracking()
                .Where(s => s.BarberStoreOwnerId == currentUserId)
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.Type,
                    s.Latitude,
                    s.Longitude,
                    s.AddressDescription,
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreMineDto>();

            var storeIds = stores.Select(s => s.Id).ToList();

            // 2) Rating & ReviewCount
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => storeIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();

            var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new
            {
                x.AvgRating,
                x.ReviewCount
            });

            // 3) Favoriler
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId))
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    FavoriteCount = g.Count()
                })
                .ToListAsync();

            var favoriteDict = favoriteStats.ToDictionary(x => x.StoreId, x => x.FavoriteCount);

            // 4) Hizmetler
            var offeringGroups = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => storeIds.Contains(o.OwnerId))
                .GroupBy(o => o.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Offerings = g.Select(o => new ServiceOfferingGetDto
                    {
                        Id = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    }).ToList()
                })
                .ToListAsync();

            var offeringDict = offeringGroups.ToDictionary(x => x.OwnerId, x => x.Offerings);

            // 5) Çalışma saatleri
            var hourGroups = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => storeIds.Contains(w.OwnerId))
                .GroupBy(w => w.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Hours = g.ToList()    // WorkingHour entity listesi
                })
                .ToListAsync();

            var hoursDict = hourGroups.ToDictionary(x => x.OwnerId, x => x.Hours);

            // 6) Görseller
            var imageGroups = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store
                         && storeIds.Contains(i.ImageOwnerId))
                .GroupBy(i => i.ImageOwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Images = g.Select(i => new ImageGetDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl
                    }).ToList()
                })
                .ToListAsync();

            var imageDict = imageGroups.ToDictionary(x => x.OwnerId, x => x.Images);

            // 7) Hepsini BarberStoreMineDto’ya projekte et
            var result = stores
                .Select(s =>
                {
                    ratingDict.TryGetValue(s.Id, out var ratingInfo);
                    favoriteDict.TryGetValue(s.Id, out var favCount);
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    hoursDict.TryGetValue(s.Id, out var hours);
                    imageDict.TryGetValue(s.Id, out var images);

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;

                    var isOpenNow = hours != null
                        ? OpenControl.IsOpenNow(hours, nowLocal)
                        : false;

                    return new BarberStoreMineDto
                    {
                        Id = s.Id,
                        StoreName = s.StoreName,
                        ImageList = images ?? new List<ImageGetDto>(),
                        Type = s.Type,
                        Rating = Math.Round(avgRating, 2),
                        FavoriteCount = favoriteCount,
                        ReviewCount = reviewCount,
                        IsOpenNow = isOpenNow,
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        AddressDescription = s.AddressDescription,
                    };
                })
                .ToList();

            return result;
        }

        public async Task<List<BarberStoreGetDto>> GetNearbyStoresAsync(double lat, double lon, double radiusKm = 1)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }   // Windows
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }      // Linux
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(lat, lon, radiusKm);
            var stores = await _context.BarberStores
                .AsNoTracking()
                .Where(s => s.Latitude >= minLat && s.Latitude <= maxLat
                         && s.Longitude >= minLon && s.Longitude <= maxLon)
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.Latitude,
                    s.Longitude,
                    s.PricingType,
                    s.PricingValue,
                    s.Type,
                    s.AddressDescription
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreGetDto>();
            var storeIds = stores.Select(s => s.Id).ToList();
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => storeIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();

            var ratingDict = ratingStats
                .ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId))
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    FavoriteCount = g.Count()
                })
                .ToListAsync();

            var favoriteDict = favoriteStats
                .ToDictionary(x => x.StoreId, x => x.FavoriteCount);
            var offeringGroups = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => storeIds.Contains(o.OwnerId))
                .GroupBy(o => o.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Offerings = g
                        .Select(o => new ServiceOfferingGetDto
                        {
                            Id = o.Id,
                            ServiceName = o.ServiceName,
                            Price = o.Price
                        })
                        .ToList()
                })
                .ToListAsync();
            var offeringDict = offeringGroups
                .ToDictionary(x => x.OwnerId, x => x.Offerings);
            var hourGroups = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => storeIds.Contains(w.OwnerId))
                .GroupBy(w => w.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Hours = g.ToList()
                })
                .ToListAsync();
            var hoursDict = hourGroups
                .ToDictionary(x => x.OwnerId, x => x.Hours);
            var imageGroups = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store
                         && storeIds.Contains(i.ImageOwnerId))
                .GroupBy(i => i.ImageOwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Images = g.Select(i => new ImageGetDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl
                    }).ToList()
                })
                .ToListAsync();
            var imageDict = imageGroups
                .ToDictionary(x => x.OwnerId, x => x.Images);
            var result = stores
                .Select(s =>
                {
                    var distance = Geo.DistanceKm(lat, lon, s.Latitude, s.Longitude);
                    if (distance > radiusKm) return null;

                    ratingDict.TryGetValue(s.Id, out var ratingInfo);
                    favoriteDict.TryGetValue(s.Id, out var favCount);
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    hoursDict.TryGetValue(s.Id, out var hours);
                    imageDict.TryGetValue(s.Id, out var images);

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;

                    var isOpenNow = hours != null
                        ? OpenControl.IsOpenNow(hours, nowLocal)
                        : false;

                    return new BarberStoreGetDto
                    {
                        Id = s.Id,
                        StoreName = s.StoreName,
                        ImageList = images ?? new List<ImageGetDto>(),
                        PricingType = s.PricingType.ToString(),
                        PricingValue = s.PricingValue,
                        Type = s.Type,
                        IsOpenNow = isOpenNow,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        AddressDescription = s.AddressDescription,
                        FavoriteCount = favoriteCount,
                        ReviewCount = reviewCount,
                        Rating = Math.Round(avgRating, 2),
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        DistanceKm = Math.Round(distance, 3)
                    };
                })
                .Where(dto => dto != null)
                .OrderBy(dto => dto!.DistanceKm)
                .ThenByDescending(dto => dto!.Rating)
                .ToList()!;

            return result;
        }

       
    }
}
