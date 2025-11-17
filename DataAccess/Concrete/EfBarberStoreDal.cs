using Core.DataAccess.EntityFramework;
using Core.Utilities;
using Core.Utilities.Helpers;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DataAccess.Concrete
{
    public class EfBarberStoreDal : EfEntityRepositoryBase<BarberStore, DatabaseContext>, IBarberStoreDal
    {
        private readonly DatabaseContext _context;
        public EfBarberStoreDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }
        public async Task<List<BarberStoreMineDto>> GetMineStores(Guid currentUserId)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }   // Windows
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }      // Linux
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            // 1) Bu kullanıcıya ait dükkanları çek
            var stores = await _context.BarberStores
                .AsNoTracking()
                .Where(s => s.BarberStoreOwnerId == currentUserId)
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.Latitude,
                    s.Longitude,
                    s.PricingType,
                    s.PricingValue,
                    s.Type
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

                    var workingHourDtos = hours != null
                        ? hours.Select(w => new WorkingHourDto
                        {
                            Id = w.Id,
                            OwnerId = w.OwnerId,
                            DayOfWeek = w.DayOfWeek,
                            StartTime = w.StartTime,
                            EndTime = w.EndTime,
                            IsClosed = w.IsClosed
                        })
                            .ToList()
                        : new List<WorkingHourDto>();

                    return new BarberStoreMineDto
                    {
                        Id = s.Id,
                        StoreName = s.StoreName,
                        StoreImageUrl = images ?? new List<ImageGetDto>(),
                        Type = s.Type,
                        PricingType = s.PricingType.ToString(),
                        PricingValue = s.PricingValue,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        Rating = Math.Round(avgRating, 2),
                        FavoriteCount = favoriteCount,
                        ReviewCount = reviewCount,
                        IsOpenNow = isOpenNow,
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        WorkingHours = workingHourDtos
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
                    s.Type
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
