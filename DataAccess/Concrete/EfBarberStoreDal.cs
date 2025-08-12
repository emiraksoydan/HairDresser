using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
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

        public async Task<List<BarberStoreListDto>> GetNearbyStoresWithStatsAsync(double userLat, double userLng, double maxDistanceKm = 1.0)
        {
            const double EarthRadiusKm = 6371;
            var userLatStr = userLat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var userLngStr = userLng.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var stores = await _context.Set<BarberStoreListDto>().FromSqlInterpolated($@"
            SELECT bs.Id, bs.StoreName, bs.StoreImageUrl, bs.Type, bs.PricingType,bs.PricingValue,
           ISNULL(favCounts.FavoriteCount, 0) as FavoriteCount,
           ISNULL(ratingStats.AvgRating, 0) as Rating,
           ISNULL(ratingStats.CommentCount, 0) as ReviewCount,
           {EarthRadiusKm} * ACOS(
             COS(RADIANS({userLat})) * COS(RADIANS(a.Latitude)) *
             COS(RADIANS(a.Longitude) - RADIANS({userLng})) +
             SIN(RADIANS({userLat})) * SIN(RADIANS(a.Latitude))
           ) as DistanceKm
            FROM [dbo].[BarberStores] bs
            INNER JOIN [dbo].[AddressInfos] a ON a.Id = bs.AddressId
            LEFT JOIN (
                SELECT FavoritedToId, COUNT(*) AS FavoriteCount
                FROM [dbo].[Favorites]
                GROUP BY FavoritedToId
            ) favCounts ON favCounts.FavoritedToId = bs.BarberStoreUserId
            LEFT JOIN (
                SELECT TargetId, AVG(CAST(Score AS FLOAT)) AS AvgRating, COUNT(Comment) AS CommentCount
                FROM [dbo].[Ratings]
                GROUP BY TargetId
    ) ratingStats ON ratingStats.TargetId = bs.BarberStoreUserId
    WHERE
      {EarthRadiusKm} * ACOS(
        COS(RADIANS({userLat})) * COS(RADIANS(a.Latitude)) *
        COS(RADIANS(a.Longitude) - RADIANS({userLng})) +
        SIN(RADIANS({userLat})) * SIN(RADIANS(a.Latitude))
      ) <= {maxDistanceKm}
").ToListAsync();

            var now = DateTime.Now;
            var dayOfWeek = (int)now.DayOfWeek;
            var currentTime = now.TimeOfDay;
            var storeIds = stores.Select(s => s.Id).ToList();
            var workingHours = await _context.WorkingHours
            .Where(wh => storeIds.Contains(wh.OwnerId))
            .AsNoTracking()
            .ToListAsync();
            foreach (var store in stores)
            {
                var todayWorkingHours = workingHours
                    .Where(wh => wh.OwnerId == store.Id && (int)wh.DayOfWeek == dayOfWeek)
                    .ToList();
                if (!todayWorkingHours.Any() || todayWorkingHours.All(wh => wh.IsClosed))
                {
                    store.IsOpenNow = false;
                }
                else
                {
                    store.IsOpenNow = todayWorkingHours.Any(wh =>
                        currentTime >= wh.StartTime && currentTime <= wh.EndTime);
                }
                var serviceOfferings = await _context.ServiceOfferings
                    .Where(s => s.OwnerId == store.Id)
                    .Select(s => new ServiceOfferingListDto
                    {
                        Id = s.Id,
                        Price = s.Price,
                        ServiceName = s.ServiceName
                    })
                    .ToListAsync();
                store.ServiceOfferings = serviceOfferings;
            }
            return stores;
        }
        public async Task<BarberStoreDetailDto> GetByIdWithStatsAsync(Guid id)
        {
            var dto = await _context.BarberStores
                .Where(bs => bs.Id == id)
                .Select(bs => new BarberStoreDetailDto
                {
                    Id = bs.Id,
                    StoreName = bs.StoreName,
                    StoreImageUrl = bs.StoreImageUrl,
                    Type = bs.Type,
                    PricingType = bs.PricingType,
                    PricingValue = bs.PricingValue,
                    Address = bs.Address,
                    FavoriteCount = _context.Favorites.Count(f => f.FavoritedToId == bs.BarberStoreUserId),
                    Rating = _context.Ratings
                        .Where(r => r.TargetId == bs.BarberStoreUserId)
                        .Average(r => (double?)r.Score) ?? 0,
                    ServiceOfferings = _context.ServiceOfferings
                    .Where(o => o.OwnerId == bs.Id)
                    .Select(o => new ServiceOfferingListDto
                    {
                        Id = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    }).ToList(),
                    WorkingHours = _context.WorkingHours
                    .Where(wh => wh.OwnerId == bs.Id)
                    .Select(wh => new WorkingHourDto
                    {
                        Id = wh.Id,
                        DayOfWeek = wh.DayOfWeek,
                        StartTime = wh.StartTime,
                        EndTime = wh.EndTime,
                        IsClosed = wh.IsClosed
                    }).ToList()

                })
                .FirstOrDefaultAsync();
            return dto;
        }
        public async Task<List<BarberStoreDetailDto>> GetByCurrentUserWithStatsAsync(Guid userId)
        {
            var stores = await _context.BarberStores
                .Where(bs => bs.BarberStoreUserId == userId)
                .Select(bs => new BarberStoreDetailDto
                {
                    Id = bs.Id,
                    StoreName = bs.StoreName,
                    StoreImageUrl = bs.StoreImageUrl,
                    Type = bs.Type,
                    PricingType = bs.PricingType,
                    PricingValue = bs.PricingValue,
                    Address = bs.Address,
                    FavoriteCount = _context.Favorites.Count(f => f.FavoritedToId == bs.BarberStoreUserId),
                    Rating = _context.Ratings
                        .Where(r => r.TargetId == bs.BarberStoreUserId)
                        .Average(r => (double?)r.Score) ?? 0,
                    ReviewCount = _context.Ratings
                     .Count(r => r.TargetId == bs.BarberStoreUserId && !string.IsNullOrEmpty(r.Comment)),
                    ServiceOfferings = _context.ServiceOfferings
                    .Where(o => o.OwnerId == bs.Id)
                    .Select(o => new ServiceOfferingListDto
                    {
                        Id = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    }).ToList(),
                    WorkingHours = _context.WorkingHours
                    .Where(wh => wh.OwnerId == bs.Id)
                    .Select(wh => new WorkingHourDto
                    {
                        Id = wh.Id,
                        DayOfWeek = wh.DayOfWeek,
                        StartTime = wh.StartTime,
                        EndTime = wh.EndTime,
                        IsClosed = wh.IsClosed
                    }).ToList()
                })
                .ToListAsync();

            var now = DateTime.Now;
            var currentDay = (int)now.DayOfWeek;
            var currentTime = now.TimeOfDay;
            foreach (var store in stores)
            {
                var todayHours = store.WorkingHours
                  .Where(wh => (int)wh.DayOfWeek == currentDay)
                  .ToList();

                if (!todayHours.Any() || todayHours.All(wh => wh.IsClosed))
                {
                    store.IsOpenNow = false;
                }
                else
                {
                    store.IsOpenNow = todayHours.Any(wh =>
                        currentTime >= wh.StartTime && currentTime <= wh.EndTime);
                }
            }
            return stores;
        }

        public async Task<BarberStoreOperationDetail> GetByIdStoreOperation(Guid id)
        {
            var manualBarberIdsFromChairs = await _context.BarberChairs
                .Where(ch => ch.StoreId == id && ch.ManualBarberId != null)
                .Select(ch => ch.ManualBarberId!.Value)
                .Distinct()
                .ToListAsync();

            var manualBarbers = await _context.ManuelBarbers
                .Where(mb => mb.StoreId == id || manualBarberIdsFromChairs.Contains(mb.Id))
                .Select(mb => new ManuelBarberOperation
                {
                    Id = mb.Id,
                    Name = mb.FirstName,
                    Surname = mb.LastName,
                    ProfileImageUrl = mb.ProfileImageUrl
                })
                .Distinct()
                .ToListAsync();

            var dto = await _context.BarberStores
               .Where(bs => bs.Id == id)
               .Select(bs => new BarberStoreOperationDetail
               {
                   Id = bs.Id,
                   StoreName = bs.StoreName,
                   StoreImageUrl = bs.StoreImageUrl,
                   Type = bs.Type,
                   PricingType = bs.PricingType,
                   PricingValue = bs.PricingValue,
                   Address = bs.Address,
                   BarberChairs = _context.BarberChairs
                .Where(ch => ch.StoreId == bs.Id)
                .Select(ch => new BarberChairDto
                {
                    Id = ch.Id,
                    Name = ch.Name,
                    ManualBarberId = ch.ManualBarberId
                }).ToList(),
                   ServiceOfferings = _context.ServiceOfferings
                   .Where(o => o.OwnerId == bs.Id)
                   .Select(o => new ServiceOfferingListDto
                   {
                       Id = o.Id,
                       ServiceName = o.ServiceName,
                       Price = o.Price
                   }).ToList(),
                   WorkingHours = _context.WorkingHours
                   .Where(wh => wh.OwnerId == bs.Id)
                   .Select(wh => new WorkingHourDto
                   {
                       Id = wh.Id,
                       DayOfWeek = wh.DayOfWeek,
                       StartTime = wh.StartTime,
                       EndTime = wh.EndTime,
                       IsClosed = wh.IsClosed
                   }).ToList(),
                   BarberOperations = manualBarbers,

               })
               .FirstOrDefaultAsync();
            return dto;
        }
    }
}
