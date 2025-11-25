using Core.DataAccess.EntityFramework;
using Core.Utilities.Helpers;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfFreeBarberDal : EfEntityRepositoryBase<FreeBarber, DatabaseContext>, IFreeBarberDal
    {
        private readonly DatabaseContext _context;
        public EfFreeBarberDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<FreeBarberGetDto>> GetNearbyFreeBarberAsync(double lat, double lon, double radiusKm = 1)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }   // Windows
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }      // Linux
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
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
                .Where(i => i.OwnerType == ImageOwnerType.Store
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
                        ImageList = images ?? new List<ImageGetDto>(),
                        Type = s.Type,
                        IsAvailable = s.IsAvailable,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        FullName = s.FirstName + " " + s.LastName,
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
