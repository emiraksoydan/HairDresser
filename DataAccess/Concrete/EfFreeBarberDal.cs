using Core.DataAccess.EntityFramework;
using Core.Utilities.Helpers;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;


namespace DataAccess.Concrete
{
    public class EfFreeBarberDal : EfEntityRepositoryBase<FreeBarber, DatabaseContext>, IFreeBarberDal
    {
        private readonly DatabaseContext _context;
        public EfFreeBarberDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<FreeBarberMinePanelDto> GetFreeBarberForUsers(Guid freeBarberId)
        {
            var freeBarber = await _context.FreeBarbers
              .AsNoTracking()
              .Where(b => b.Id == freeBarberId)
              .Select(s => new
              {
                  s.Id,
                  s.FreeBarberUserId,
                  s.Latitude,
                  s.Longitude,
                  s.Type,
                  s.FirstName,
                  s.LastName,
                  s.BarberCertificateImageId,
                  s.BeautySalonCertificateImageId,
                  s.IsAvailable,
              })
              .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDto();

            // Rating - Artık TargetId User ID
            var avgRating = await _context.Ratings
            .AsNoTracking()
            .Where(r => r.TargetId == freeBarber.FreeBarberUserId)
            .Select(r => (double?)r.Score)
            .AverageAsync() ?? 0.0;

            var reviewCount = await _context.Ratings
                .AsNoTracking()
                .CountAsync(r => r.TargetId == freeBarber.FreeBarberUserId);

            // Favorite count (sadece aktif favoriler) - Artık User ID'ler arasında
            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == freeBarber.FreeBarberUserId && f.IsActive);


            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber && i.Id != freeBarber.BarberCertificateImageId && i.Id != freeBarber.BeautySalonCertificateImageId)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == freeBarber.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            return new FreeBarberMinePanelDto
            {
                Id = freeBarber.Id,
                FreeBarberUserId = freeBarber.FreeBarberUserId,
                Type = freeBarber.Type,
                FullName = freeBarber.FirstName + " " + freeBarber.LastName,
                IsAvailable = freeBarber.IsAvailable,
                ImageList = images,
                Offerings = offerings,
                FavoriteCount = favoriteCount,
                Rating = avgRating,
                ReviewCount = reviewCount,
                BeautySalonCertificateImageId = freeBarber.BeautySalonCertificateImageId,
            };
        }

        public async Task<FreeBarberMinePanelDto> GetMyPanel(Guid currentUserId)
        {
            var freeBarber = await _context.FreeBarbers
               .AsNoTracking()
               .Where(b => b.FreeBarberUserId == currentUserId)
               .Select(s => new
               {
                   s.Id,
                   s.FreeBarberUserId,
                   s.Latitude,
                   s.Longitude,
                   s.Type,
                   s.FirstName,
                   s.LastName,
                   s.BarberCertificateImageId,
                   s.BeautySalonCertificateImageId,
                   s.IsAvailable,



               })
               .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDto();

            // Rating - Artık TargetId User ID
            var avgRating = await _context.Ratings
            .AsNoTracking()
            .Where(r => r.TargetId == freeBarber.FreeBarberUserId)
            .Select(r => (double?)r.Score)
            .AverageAsync() ?? 0.0;

            var reviewCount = await _context.Ratings
                .AsNoTracking()
                .CountAsync(r => r.TargetId == freeBarber.FreeBarberUserId);

            // Favorite count (sadece aktif favoriler) - Artık User ID'ler arasında
            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == freeBarber.FreeBarberUserId && f.IsActive);


            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber && i.Id != freeBarber.BarberCertificateImageId && i.Id != freeBarber.BeautySalonCertificateImageId)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == freeBarber.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            return new FreeBarberMinePanelDto
            {
                Id = freeBarber.Id,
                FreeBarberUserId = freeBarber.FreeBarberUserId,
                Type = freeBarber.Type,
                FullName = freeBarber.FirstName + " " + freeBarber.LastName,
                IsAvailable = freeBarber.IsAvailable,
                ImageList = images,
                Offerings = offerings,
                FavoriteCount = favoriteCount,
                Rating = avgRating,
                ReviewCount = reviewCount,
                Latitude = freeBarber.Latitude,
                Longitude = freeBarber.Longitude,
                BeautySalonCertificateImageId = freeBarber.BeautySalonCertificateImageId,
            };
        }

        public async Task<List<FreeBarberGetDto>> GetNearbyFreeBarberAsync(double lat, double lon, double radiusKm = 10, Guid? currentUserId = null)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(lat, lon, radiusKm);
            var freeBarbers = await _context.FreeBarbers
                .AsNoTracking()
                .Where(s => s.Latitude >= minLat && s.Latitude <= maxLat
                         && s.Longitude >= minLon && s.Longitude <= maxLon)
                .Select(s => new
                {
                    s.Id,
                    s.Latitude,
                    s.Longitude,
                    s.Type,
                    s.FirstName,
                    s.LastName,
                    s.FreeBarberUserId,
                    s.IsAvailable,
                    s.BarberCertificateImageId,
                    s.BeautySalonCertificateImageId

                })
                .ToListAsync();

            if (!freeBarbers.Any())
                return new List<FreeBarberGetDto>();
            var freeBarberIds = freeBarbers.Select(s => s.Id).ToList();
            // Rating - TargetId = FreeBarber User ID (FreeBarber'ın rating'i)
            var freeBarberOwnerIds = freeBarbers.Select(s => s.FreeBarberUserId).Distinct().ToList();
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => freeBarberOwnerIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    OwnerUserId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()

                })
                .ToListAsync();

            var ratingDict = ratingStats
                .ToDictionary(x => x.OwnerUserId, x => new { x.AvgRating, x.ReviewCount });
            
            // Favorite count (sadece aktif favoriler) - FavoritedToId = FreeBarber User ID (FreeBarber'ın favori sayısı)
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => freeBarberOwnerIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    OwnerUserId = g.Key,
                    FavoriteCount = g.Count(f => f.IsActive)
                })
                .ToListAsync();

            var favoriteDict = favoriteStats
                .ToDictionary(x => x.OwnerUserId, x => x.FavoriteCount);

            // User IsFavorited bilgisi
            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (currentUserId.HasValue)
            {
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == currentUserId.Value && freeBarberOwnerIds.Contains(f.FavoritedToId) && f.IsActive)
                    .Select(f => f.FavoritedToId)
                    .ToListAsync();
                
                isFavoritedDict = userFavs.ToDictionary(x => x, x => true);
            }

            var offeringGroups = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => freeBarberIds.Contains(o.OwnerId))
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

            var imageGroups = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.FreeBarber
                         && freeBarberIds.Contains(i.ImageOwnerId))
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
            var result = freeBarbers
                .Select(s =>
                {
                    var distance = Geo.DistanceKm(lat, lon, s.Latitude, s.Longitude);
                    if (distance > radiusKm) return null;

                    ratingDict.TryGetValue(s.FreeBarberUserId, out var ratingInfo); // Artık owner User ID'ye göre
                    favoriteDict.TryGetValue(s.FreeBarberUserId, out var favCount); // Artık owner User ID'ye göre
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    imageDict.TryGetValue(s.Id, out var images);

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;
                    var isFavorited = isFavoritedDict.GetValueOrDefault(s.FreeBarberUserId, false);

                    return new FreeBarberGetDto
                    {
                        Id = s.Id,
                        FreeBarberUserId = s.FreeBarberUserId,
                        IsAvailable = s.IsAvailable,
                        ImageList = images?.Where(img => img.Id != s.BarberCertificateImageId && img.Id != s.BeautySalonCertificateImageId).ToList() ?? new List<ImageGetDto>(),
                        Type = s.Type,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        FullName = s.FirstName + " " + s.LastName,
                        FavoriteCount = favoriteCount,
                        ReviewCount = reviewCount,
                        Rating = Math.Round(avgRating, 2),
                        Offerings = offerings ?? new List<ServiceOfferingGetDto>(),
                        DistanceKm = Math.Round(distance, 3),
                        IsFavorited = isFavorited,
                        BeautySalonCertificateImageId = s.BeautySalonCertificateImageId
                    };
                })
                .Where(dto => dto != null)
                .OrderBy(dto => dto!.DistanceKm)
                .ThenByDescending(dto => dto!.Rating)
                .ToList()!;

            return result;
        }

        public async Task<FreeBarberMinePanelDetailDto> GetPanelDetailById(Guid panelId)
        {
            var freeBarber = await _context.FreeBarbers
               .AsNoTracking()
               .Where(b => b.Id == panelId)
               .Select(s => new
               {
                   s.Id,
                   s.FreeBarberUserId,
                   s.Type,
                   s.FirstName,
                   s.LastName,
                   s.BarberCertificateImageId,
                   s.BeautySalonCertificateImageId,
                   s.IsAvailable,
                   s.Latitude,
                   s.Longitude,

               })
               .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDetailDto();

            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber && i.Id != freeBarber.BarberCertificateImageId && i.Id != freeBarber.BeautySalonCertificateImageId)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == freeBarber.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            // Fetch barber certificate image if exists
            ImageGetDto certificateImageDto = null;
            if (freeBarber.BarberCertificateImageId.HasValue)
            {
                var certImage = await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == freeBarber.BarberCertificateImageId.Value);

                if (certImage != null)
                {
                    certificateImageDto = new ImageGetDto
                    {
                        Id = certImage.Id,
                        ImageUrl = certImage.ImageUrl,
                    };
                }
            }

            // Fetch beauty salon certificate image if exists
            ImageGetDto beautySalonCertificateImageDto = null;
            if (freeBarber.BeautySalonCertificateImageId.HasValue)
            {
                var beautyCertImage = await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == freeBarber.BeautySalonCertificateImageId.Value);

                if (beautyCertImage != null)
                {
                    beautySalonCertificateImageDto = new ImageGetDto
                    {
                        Id = beautyCertImage.Id,
                        ImageUrl = beautyCertImage.ImageUrl,
                    };
                }
            }

            return new FreeBarberMinePanelDetailDto
            {
                Id = freeBarber.Id,
                FreeBarberUserId = freeBarber.FreeBarberUserId,
                Type = freeBarber.Type,
                FirstName = freeBarber.FirstName,
                LastName = freeBarber.LastName,
                IsAvailable = freeBarber.IsAvailable,
                BarberCertificateImageId = freeBarber.BarberCertificateImageId,
                BarberCertificateImage = certificateImageDto,
                BeautySalonCertificateImageId = freeBarber.BeautySalonCertificateImageId,
                BeautySalonCertificateImage = beautySalonCertificateImageDto,
                ImageList = images,
                Offerings = offerings,
                Latitude = freeBarber.Latitude,
                Longitude = freeBarber.Longitude,
            };
        }

        public async Task<bool> TryLockAsync(Guid freeBarberUserId)
        {
            var rows = await _context.FreeBarbers
                .Where(x => x.FreeBarberUserId == freeBarberUserId && x.IsAvailable)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsAvailable, false)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
            return rows > 0;
        }

        public async Task<List<FreeBarberGetDto>> GetFilteredFreeBarbersAsync(FilterRequestDto filter)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            
            // Base query
            var query = _context.FreeBarbers.AsNoTracking().AsQueryable();

            // 0. Kullanıcının kendi FreeBarber panelinin ID'sini al (IsOwnPanel için kullanılacak)
            Guid? ownFreeBarberPanelId = null;
            if (filter.CurrentUserId.HasValue)
            {
                ownFreeBarberPanelId = await _context.FreeBarbers
                    .AsNoTracking()
                    .Where(fb => fb.FreeBarberUserId == filter.CurrentUserId.Value)
                    .Select(fb => fb.Id)
                    .FirstOrDefaultAsync();
            }

            // 1. Konum filtresi (nearby) - kendi paneli konum filtresinden muaf tutulur
            if (filter.Latitude.HasValue && filter.Longitude.HasValue)
            {
                var distance = filter.DistanceKm > 0 ? filter.DistanceKm : 1.0;
                var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(
                    filter.Latitude.Value, 
                    filter.Longitude.Value, 
                    distance
                );
                // Kendi paneli her zaman dahil edilir - konum filtresi sadece başkalarının panellerine uygulanır
                query = query.Where(fb => 
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) || // Kendi paneli her zaman dahil
                    (fb.Latitude >= minLat && fb.Latitude <= maxLat &&
                     fb.Longitude >= minLon && fb.Longitude <= maxLon)
                );
            }

            // 2. İsim araması
            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var searchLower = filter.SearchQuery.ToLower();
                query = query.Where(fb => 
                    (fb.FirstName + " " + fb.LastName).ToLower().Contains(searchLower)
                );
            }

            // 3. Ana kategori filtresi (BarberType)
            if (filter.MainCategory.HasValue)
            {
                query = query.Where(fb => fb.Type == filter.MainCategory.Value);
            }

            // 4. Müsaitlik filtresi
            if (filter.IsAvailable.HasValue)
            {
                query = query.Where(fb => fb.IsAvailable == filter.IsAvailable.Value);
            }

            // FreeBarber bilgilerini al
            var freeBarbers = await query
                .Select(fb => new
                {
                    fb.Id,
                    fb.FreeBarberUserId,
                    fb.FirstName,
                    fb.LastName,
                    fb.Latitude,
                    fb.Longitude,
                    fb.Type,
                    fb.IsAvailable,
                    fb.BarberCertificateImageId,
                    fb.BeautySalonCertificateImageId,
                    IsOwnPanel = ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value
                })
                .ToListAsync();

            if (!freeBarbers.Any())
                return new List<FreeBarberGetDto>();

            var freeBarberIds = freeBarbers.Select(fb => fb.Id).ToList();
            var freeBarberUserIds = freeBarbers.Select(fb => fb.FreeBarberUserId).Distinct().ToList();

            // Offerings
            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => freeBarberIds.Contains(o.OwnerId))
                .Select(o => new
                {
                    o.OwnerId,
                    o.Id,
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

            // 5. Hizmet filtresi (CategoryId listesi)
            if (filter.ServiceIds != null && filter.ServiceIds.Any())
            {
                var categoryNames = await _context.Categories
                    .AsNoTracking()
                    .Where(c => filter.ServiceIds.Contains(c.Id))
                    .Select(c => c.Name)
                    .ToListAsync();

                var freeBarbersWithServices = offerings
                    .Where(o => filter.ServiceIds.Contains(o.Id) || categoryNames.Contains(o.Offering.ServiceName))
                    .Select(o => o.OwnerId)
                    .Distinct()
                    .ToList();

                freeBarbers = freeBarbers.Where(fb => freeBarbersWithServices.Contains(fb.Id)).ToList();
                freeBarberIds = freeBarbers.Select(fb => fb.Id).ToList();
                freeBarberUserIds = freeBarbers.Select(fb => fb.FreeBarberUserId).Distinct().ToList();
            }

            // 6. Fiyat filtresi (min offering price)
            if (filter.MinPrice.HasValue || filter.MaxPrice.HasValue)
            {
                var validFreeBarbers = new List<Guid>();
                
                foreach (var fb in freeBarbers)
                {
                    var fbOfferings = offeringsDict.GetValueOrDefault(fb.Id, new List<ServiceOfferingGetDto>());
                    if (!fbOfferings.Any()) continue;
                    
                    var minPrice = fbOfferings.Min(o => o.Price);
                    
                    bool matches = true;
                    if (filter.MinPrice.HasValue && minPrice < filter.MinPrice.Value)
                        matches = false;
                    if (filter.MaxPrice.HasValue && minPrice > filter.MaxPrice.Value)
                        matches = false;
                    
                    if (matches)
                        validFreeBarbers.Add(fb.Id);
                }
                
                freeBarbers = freeBarbers.Where(fb => validFreeBarbers.Contains(fb.Id)).ToList();
                freeBarberIds = freeBarbers.Select(fb => fb.Id).ToList();
                freeBarberUserIds = freeBarbers.Select(fb => fb.FreeBarberUserId).Distinct().ToList();
            }

            // Rating bilgileri
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => freeBarberUserIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    FreeBarber = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();

            var ratingDict = ratingStats.ToDictionary(x => x.FreeBarber, x => new { x.AvgRating, x.ReviewCount });

            // 7. Puanlama filtresi
            if (filter.MinRating.HasValue && filter.MinRating.Value > 0)
            {
                freeBarbers = freeBarbers.Where(fb =>
                    ratingDict.ContainsKey(fb.FreeBarberUserId) &&
                    ratingDict[fb.FreeBarberUserId].AvgRating >= filter.MinRating.Value
                ).ToList();
                freeBarberIds = freeBarbers.Select(fb => fb.Id).ToList();
                freeBarberUserIds = freeBarbers.Select(fb => fb.FreeBarberUserId).Distinct().ToList();
            }

            // Favorite bilgileri
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => freeBarberUserIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    FreeBarber = g.Key,
                    FavoriteCount = g.Count()
                })
                .ToListAsync();

            var favoriteDict = favoriteStats.ToDictionary(x => x.FreeBarber, x => x.FavoriteCount);

            // 8. Favori filtresi
            if (filter.FavoritesOnly.HasValue && filter.FavoritesOnly.Value && filter.CurrentUserId.HasValue)
            {
                var userFavorites = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == filter.CurrentUserId.Value && f.IsActive && freeBarberUserIds.Contains(f.FavoritedToId))
                    .Select(f => f.FavoritedToId)
                    .ToListAsync();
                
                freeBarbers = freeBarbers.Where(fb => userFavorites.Contains(fb.FreeBarberUserId)).ToList();
                freeBarberIds = freeBarbers.Select(fb => fb.Id).ToList();
                freeBarberUserIds = freeBarbers.Select(fb => fb.FreeBarberUserId).Distinct().ToList();
            }

            // User IsFavorited bilgisi
            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (filter.CurrentUserId.HasValue)
            {
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == filter.CurrentUserId.Value && freeBarberUserIds.Contains(f.FavoritedToId))
                    .Select(f => new { f.FavoritedToId, f.IsActive })
                    .ToListAsync();
                
                isFavoritedDict = userFavs.ToDictionary(x => x.FavoritedToId, x => x.IsActive);
            }

            // Images
            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.FreeBarber && freeBarberIds.Contains(i.ImageOwnerId))
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
            var result = freeBarbers.Select(fb =>
            {
                var rating = ratingDict.GetValueOrDefault(fb.FreeBarberUserId);
                var fbOfferings = offeringsDict.GetValueOrDefault(fb.Id, new List<ServiceOfferingGetDto>());
                
                return new FreeBarberGetDto
                {
                    Id = fb.Id,
                    FreeBarberUserId = fb.FreeBarberUserId,
                    FullName = $"{fb.FirstName} {fb.LastName}",
                    Latitude = fb.Latitude,
                    Longitude = fb.Longitude,
                    Type = fb.Type,
                    IsAvailable = fb.IsAvailable,
                    Rating = rating != null ? Math.Round(rating.AvgRating, 2) : 0,
                    ReviewCount = rating?.ReviewCount ?? 0,
                    FavoriteCount = favoriteDict.GetValueOrDefault(fb.FreeBarberUserId, 0),
                    IsFavorited = isFavoritedDict.GetValueOrDefault(fb.FreeBarberUserId, false),
                    Offerings = fbOfferings,
                    ImageList = imagesDict.GetValueOrDefault(fb.Id, new List<ImageGetDto>()).Where(img => img.Id != fb.BarberCertificateImageId && img.Id != fb.BeautySalonCertificateImageId).ToList(),
                    IsOwnPanel = fb.IsOwnPanel, // Kendi paneli mi bilgisi (frontend'de kullanılabilir)
                    BeautySalonCertificateImageId = fb.BeautySalonCertificateImageId
                };
            }).ToList();

            // 9. Fiyat sıralaması (min offering price bazlı)
            if (!string.IsNullOrWhiteSpace(filter.PriceSort) && filter.PriceSort != "none")
            {
                result = filter.PriceSort == "asc"
                    ? result.OrderBy(fb => fb.Offerings.Any() ? fb.Offerings.Min(o => o.Price) : decimal.MaxValue).ToList()
                    : result.OrderByDescending(fb => fb.Offerings.Any() ? fb.Offerings.Min(o => o.Price) : 0).ToList();
            }

            return result;
        }
    }
}

