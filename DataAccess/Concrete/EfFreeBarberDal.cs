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
                  s.BarberCertificate,
                  s.IsAvailable,
              })
              .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDto();

            var avgRating = await _context.Ratings
            .AsNoTracking()
            .Where(r => r.TargetId == freeBarber.Id)
            .Select(r => (double?)r.Score)
            .AverageAsync() ?? 0.0;

            var reviewCount = await _context.Ratings
                .AsNoTracking()
                .CountAsync(r => r.TargetId == freeBarber.Id);

            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == freeBarber.Id);


            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber)
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
                Type = freeBarber.Type,
                FullName = freeBarber.FirstName + " " + freeBarber.LastName,
                IsAvailable = freeBarber.IsAvailable,
                ImageList = images,
                Offerings = offerings,
                FavoriteCount = favoriteCount,
                Rating = avgRating,
                ReviewCount = reviewCount,
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
                   s.BarberCertificate,
                   s.IsAvailable,



               })
               .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDto();

            var avgRating = await _context.Ratings
            .AsNoTracking()
            .Where(r => r.TargetId == freeBarber.Id)
            .Select(r => (double?)r.Score)
            .AverageAsync() ?? 0.0;

            var reviewCount = await _context.Ratings
                .AsNoTracking()
                .CountAsync(r => r.TargetId == freeBarber.Id);

            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == freeBarber.Id);

  
            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber)
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
            };
        }

        public async Task<List<FreeBarberGetDto>> GetNearbyFreeBarberAsync(double lat, double lon, double radiusKm = 1)
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

                })
                .ToListAsync();

            if (!freeBarbers.Any())
                return new List<FreeBarberGetDto>();
            var freeBarberIds = freeBarbers.Select(s => s.Id).ToList();
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => freeBarberIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    FreeBarberId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()

                })
                .ToListAsync();

            var ratingDict = ratingStats
                .ToDictionary(x => x.FreeBarberId, x => new { x.AvgRating, x.ReviewCount });
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => freeBarberIds.Contains(f.FavoritedToId))
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    FreeBarberId = g.Key,
                    FavoriteCount = g.Count()
                })
                .ToListAsync();

            var favoriteDict = favoriteStats
                .ToDictionary(x => x.FreeBarberId, x => x.FavoriteCount);
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

                    ratingDict.TryGetValue(s.Id, out var ratingInfo);
                    favoriteDict.TryGetValue(s.Id, out var favCount);
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    imageDict.TryGetValue(s.Id, out var images);

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;

                    return new FreeBarberGetDto
                    {
                        Id = s.Id,
                        IsAvailable = s.IsAvailable,
                        ImageList = images ?? new List<ImageGetDto>(),
                        Type = s.Type,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        FullName = s.FirstName + " " + s.LastName,
                        FavoriteCount = favoriteCount,
                        ReviewCount = reviewCount,
                        Rating = Math.Round(avgRating, 2),
                        Offerings = offerings ?? new List<ServiceOfferingGetDto>(),
                        DistanceKm = Math.Round(distance, 3)
                        
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
                   s.BarberCertificate,
                   s.IsAvailable,
                   s.Latitude,
                   s.Longitude,

               })
               .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDetailDto();

            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber)
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

            return new FreeBarberMinePanelDetailDto
            {
                Id = freeBarber.Id,
                Type = freeBarber.Type,
                FirstName = freeBarber.FirstName,
                LastName = freeBarber.LastName,
                IsAvailable = freeBarber.IsAvailable,
                BarberCertificate = freeBarber.BarberCertificate,
                ImageList = images,
                Offerings = offerings,
                Latitude = freeBarber.Latitude,
                Longitude = freeBarber.Longitude,
            };
        }
    }
}
