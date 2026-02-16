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
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

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
                    s.BarberStoreOwnerId, // Owner User ID'yi de al
                })
                .FirstOrDefaultAsync();

            if (store == null)
                return new BarberStoreMineDto();

            // 2) Rating + review count - TargetId = Store ID (her dükkanın kendi rating'i)
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

            // 3) Favorite count (sadece aktif favoriler) - FavoritedToId = Store ID (her dükkanın kendi favori sayısı)
            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == store.Id && f.IsActive);

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
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
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

            // ✅ N+1 FIX: Manuel barberleri önce çek
            var manuelBarbers = await _context.ManuelBarbers
          .AsNoTracking()
          .Where(b => b.StoreId == storeId)
          .ToListAsync();

            var barberIds = manuelBarbers.Select(b => b.Id).ToList();

            // ✅ N+1 FIX: Tüm rating'leri tek sorguda çek ve grupla
            var barberRatings = await _context.Ratings
                .AsNoTracking()
                .Where(r => barberIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    BarberId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score)
                })
                .ToDictionaryAsync(x => x.BarberId, x => x.AvgRating);

            // ✅ N+1 FIX: Tüm image'ları tek sorguda çek
            var barberImages = await _context.Images
                .AsNoTracking()
                .Where(i => barberIds.Contains(i.ImageOwnerId))
                .GroupBy(i => i.ImageOwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    ImageUrl = g.First().ImageUrl
                })
                .ToDictionaryAsync(x => x.OwnerId, x => x.ImageUrl);

            // ✅ Memory'de birleştir (N+1 yok, sadece dictionary lookup!)
            var manuelBarberDtos = manuelBarbers
                .Select(b => new ManuelBarberDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    Rating = barberRatings.ContainsKey(b.Id) ? barberRatings[b.Id] : 0,
                    ProfileImageUrl = barberImages.ContainsKey(b.Id) ? barberImages[b.Id] : null
                })
                .ToList();


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

            // Tax document image'ı çek
            ImageGetDto taxDocumentImageDto = null;
            if (store.TaxDocumentImageId.HasValue)
            {
                var taxImage = await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == store.TaxDocumentImageId.Value);

                if (taxImage != null)
                {
                    taxDocumentImageDto = new ImageGetDto
                    {
                        Id = taxImage.Id,
                        ImageUrl = taxImage.ImageUrl,
                    };
                }
            }

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
                TaxDocumentImageId = store.TaxDocumentImageId,
                TaxDocumentImage = taxDocumentImageDto,

            };
            return dto;
        }

        public async Task<List<BarberStoreMineDto>> GetMineStores(Guid currentUserId)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

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
                    s.BarberStoreOwnerId, // Owner User ID'yi de al
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreMineDto>();

            var storeIds = stores.Select(s => s.Id).ToList();

            // 2) Rating & ReviewCount - TargetId = Store ID (her dükkanın kendi rating'i)
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

            // 3) Favoriler - FavoritedToId = Store ID (her dükkanın kendi favori sayısı)
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId) && f.IsActive)
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

            // 7) Hepsini BarberStoreMineDto'ya projekte et
            var result = stores
                .Select(s =>
                {
                    ratingDict.TryGetValue(s.Id, out var ratingInfo); // Her store'un kendi rating'i
                    favoriteDict.TryGetValue(s.Id, out var favCount); // Her store'un kendi favori sayısı
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    hoursDict.TryGetValue(s.Id, out var hours);
                    imageDict.TryGetValue(s.Id, out var images); // Her store'un kendi fotoğrafları

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;

                    var isOpenNow = hours != null
                        ? OpenControl.IsOpenNow(hours, nowLocal)
                        : false;

                    return new BarberStoreMineDto
                    {
                        Id = s.Id,
                        StoreName = s.StoreName, // Her store'un kendi ismi
                        ImageList = images ?? new List<ImageGetDto>(), // Her store'un kendi fotoğrafları
                        Type = s.Type,
                        Rating = Math.Round(avgRating, 2), // Her store'un kendi rating'i
                        FavoriteCount = favoriteCount, // Her store'un kendi favori sayısı
                        ReviewCount = reviewCount, // Her store'un kendi review sayısı
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

        public async Task<List<BarberStoreGetDto>> GetNearbyStoresAsync(double lat, double lon, double radiusKm = 10, Guid? currentUserId = null)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
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
                    s.AddressDescription,
                    s.BarberStoreOwnerId // Owner User ID'yi de al
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreGetDto>();
            var storeIds = stores.Select(s => s.Id).ToList();
            // Rating - TargetId = Store ID (her dükkanın kendi rating'i)
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
            
            // Favorite - FavoritedToId = Store ID (her dükkanın kendi favori sayısı)
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    FavoriteCount = g.Count(f => f.IsActive)
                })
                .ToListAsync();

            var favoriteDict = favoriteStats
                .ToDictionary(x => x.StoreId, x => x.FavoriteCount);

            // User IsFavorited bilgisi
            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (currentUserId.HasValue)
            {
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == currentUserId.Value && storeIds.Contains(f.FavoritedToId) && f.IsActive)
                    .Select(f => f.FavoritedToId)
                    .ToListAsync();
                
                isFavoritedDict = userFavs.ToDictionary(x => x, x => true);
            }

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

                    ratingDict.TryGetValue(s.Id, out var ratingInfo); // Her store'un kendi rating'i
                    favoriteDict.TryGetValue(s.Id, out var favCount); // Her store'un kendi favori sayısı
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    hoursDict.TryGetValue(s.Id, out var hours);
                    imageDict.TryGetValue(s.Id, out var images);

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;
                    var isFavorited = isFavoritedDict.GetValueOrDefault(s.Id, false);

                    var isOpenNow = hours != null
                        ? OpenControl.IsOpenNow(hours, nowLocal)
                        : false;

                    return new BarberStoreGetDto
                    {
                        Id = s.Id,
                        BarberStoreOwnerId = s.BarberStoreOwnerId, // Kendi dükkanına tıklandığında güncelleme sheet'i açmak için gerekli
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
                        DistanceKm = Math.Round(distance, 3),
                        IsFavorited = isFavorited
                    };
                })
                .Where(dto => dto != null)
                .OrderBy(dto => dto!.DistanceKm)
                .ThenByDescending(dto => dto!.Rating)
                .ToList()!;

            return result;
        }

        public async Task<List<BarberStoreGetDto>> GetFilteredStoresAsync(FilterRequestDto filter)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            
            // Base query
            var query = _context.BarberStores.AsNoTracking().AsQueryable();

            // 0. Kullanıcının kendi dükkanlarının ID'lerini al (IsOwnStore için kullanılacak)
            var ownStoreIds = new List<Guid>();
            if (filter.CurrentUserId.HasValue)
            {
                ownStoreIds = await _context.BarberStores
                    .AsNoTracking()
                    .Where(s => s.BarberStoreOwnerId == filter.CurrentUserId.Value)
                    .Select(s => s.Id)
                    .ToListAsync();
            }

            // 1. Konum filtresi (nearby) - kendi dükkanları konum filtresinden muaf tutulur
            if (filter.Latitude.HasValue && filter.Longitude.HasValue)
            {
                var distance = filter.DistanceKm > 0 ? filter.DistanceKm : 1.0;
                var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(
                    filter.Latitude.Value, 
                    filter.Longitude.Value, 
                    distance
                );
                // Kendi dükkanları her zaman dahil edilir - konum filtresi sadece başkalarının dükkanlarına uygulanır
                query = query.Where(s => 
                    ownStoreIds.Contains(s.Id) || // Kendi dükkanları her zaman dahil
                    (s.Latitude >= minLat && s.Latitude <= maxLat &&
                     s.Longitude >= minLon && s.Longitude <= maxLon)
                );
            }

            // 2. İsim araması
            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var searchLower = filter.SearchQuery.ToLower();
                query = query.Where(s => s.StoreName.ToLower().Contains(searchLower));
            }

            // 3. Ana kategori filtresi (BarberType)
            if (filter.MainCategory.HasValue)
            {
                query = query.Where(s => s.Type == filter.MainCategory.Value);
            }

            // 4. Pricing Type filtresi
            if (!string.IsNullOrWhiteSpace(filter.PricingType) && filter.PricingType != "all")
            {
                if (filter.PricingType == "rent")
                    query = query.Where(s => s.PricingType == PricingType.Rent);
                else if (filter.PricingType == "percent")
                    query = query.Where(s => s.PricingType == PricingType.Percent);
            }

            // 5. Fiyat aralığı filtresi
            if (filter.MinPrice.HasValue)
                query = query.Where(s => s.PricingValue >= (double)filter.MinPrice.Value);
            
            if (filter.MaxPrice.HasValue)
                query = query.Where(s => s.PricingValue <= (double)filter.MaxPrice.Value);

            // Store bilgilerini al
            var stores = await query
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.Latitude,
                    s.Longitude,
                    s.PricingType,
                    s.PricingValue,
                    s.Type,
                    s.AddressDescription,
                    s.BarberStoreOwnerId,
                    IsOwnStore = ownStoreIds.Contains(s.Id)
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreGetDto>();

            var storeIds = stores.Select(s => s.Id).ToList();

            // 6. Hizmet filtresi (CategoryId listesi)
            if (filter.ServiceIds != null && filter.ServiceIds.Any())
            {
                var categoryNames = await _context.Categories
                    .AsNoTracking()
                    .Where(c => filter.ServiceIds.Contains(c.Id))
                    .Select(c => c.Name)
                    .ToListAsync();

                var storesWithServices = await _context.ServiceOfferings
                    .AsNoTracking()
                    .Where(o =>
                        storeIds.Contains(o.OwnerId) &&
                        (filter.ServiceIds.Contains(o.Id) || categoryNames.Contains(o.ServiceName))
                    )
                    .Select(o => o.OwnerId)
                    .Distinct()
                    .ToListAsync();

                stores = stores.Where(s => storesWithServices.Contains(s.Id)).ToList();
                storeIds = stores.Select(s => s.Id).ToList();
            }

            // Rating bilgileri
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

            var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });

            // 7. Puanlama filtresi
            if (filter.MinRating.HasValue && filter.MinRating.Value > 0)
            {
                stores = stores.Where(s => 
                    ratingDict.ContainsKey(s.Id) && ratingDict[s.Id].AvgRating >= filter.MinRating.Value
                ).ToList();
                storeIds = stores.Select(s => s.Id).ToList();
            }

            // Favorite bilgileri
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    FavoriteCount = g.Count()
                })
                .ToListAsync();

            var favoriteDict = favoriteStats.ToDictionary(x => x.StoreId, x => x.FavoriteCount);

            // 8. Favori filtresi
            if (filter.FavoritesOnly.HasValue && filter.FavoritesOnly.Value && filter.CurrentUserId.HasValue)
            {
                var userFavorites = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == filter.CurrentUserId.Value && f.IsActive && storeIds.Contains(f.FavoritedToId))
                    .Select(f => f.FavoritedToId)
                    .ToListAsync();
                
                stores = stores.Where(s => userFavorites.Contains(s.Id)).ToList();
                storeIds = stores.Select(s => s.Id).ToList();
            }

            // User IsFavorited bilgisi
            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (filter.CurrentUserId.HasValue)
            {
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == filter.CurrentUserId.Value && storeIds.Contains(f.FavoritedToId))
                    .Select(f => new { f.FavoritedToId, f.IsActive })
                    .ToListAsync();
                
                isFavoritedDict = userFavs.ToDictionary(x => x.FavoritedToId, x => x.IsActive);
            }

            // Offerings
            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => storeIds.Contains(o.OwnerId))
                .Select(o => new
                {
                    o.OwnerId,
                    Offering = new ServiceOfferingGetDto
                    {
                        Id = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    }
                })
                .ToListAsync();

            var offeringsDict = offerings
                .GroupBy(o => o.OwnerId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Offering).ToList());

            // Working hours
            var hours = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => storeIds.Contains(w.OwnerId))
                .ToListAsync();

            var hoursDict = hours
                .GroupBy(h => h.OwnerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Images
            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store && storeIds.Contains(i.ImageOwnerId))
                .Select(i => new
                {
                    i.ImageOwnerId,
                    Image = new ImageGetDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl
                    }
                })
                .ToListAsync();

            var imagesDict = images
                .GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Image).ToList());

            // DTO'ları oluştur
            var result = stores.Select(s =>
            {
                var storeHours = hoursDict.GetValueOrDefault(s.Id, new List<WorkingHour>());
                var rating = ratingDict.GetValueOrDefault(s.Id);
                
                return new BarberStoreGetDto
                {
                    Id = s.Id,
                    BarberStoreOwnerId = s.BarberStoreOwnerId,
                    StoreName = s.StoreName,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    PricingType = s.PricingType.ToString(),
                    PricingValue = s.PricingValue,
                    Type = s.Type,
                    AddressDescription = s.AddressDescription,
                    Rating = rating != null ? Math.Round(rating.AvgRating, 2) : 0,
                    ReviewCount = rating?.ReviewCount ?? 0,
                    FavoriteCount = favoriteDict.GetValueOrDefault(s.Id, 0),
                    IsFavorited = isFavoritedDict.GetValueOrDefault(s.Id, false),
                    IsOpenNow = OpenControl.IsOpenNow(storeHours, nowLocal),
                    Offerings = offeringsDict.GetValueOrDefault(s.Id, new List<ServiceOfferingGetDto>()),
                    ServiceOfferings = offeringsDict.GetValueOrDefault(s.Id, new List<ServiceOfferingGetDto>()),
                    ImageList = imagesDict.GetValueOrDefault(s.Id, new List<ImageGetDto>()),
                    IsOwnStore = s.IsOwnStore // Kendi dükkanı mı bilgisi (frontend'de kullanılabilir)
                };
            }).ToList();

            // 9. IsOpenNow filtresi (true => açık, false => kapalı)
            if (filter.IsOpenNow.HasValue)
            {
                result = result.Where(s => s.IsOpenNow == filter.IsOpenNow.Value).ToList();
            }

            // 10. Fiyat sıralaması
            if (!string.IsNullOrWhiteSpace(filter.PriceSort) && filter.PriceSort != "none")
            {
                result = filter.PriceSort == "asc"
                    ? result.OrderBy(s => s.PricingValue).ToList()
                    : result.OrderByDescending(s => s.PricingValue).ToList();
            }

            return result;
        }
       
    }
}
