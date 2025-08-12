using System;
using System.Collections.Generic;
using System.Linq;
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
    public class EfFreeBarberDal : EfEntityRepositoryBase<FreeBarber, DatabaseContext>, IFreeBarberDal
    {
        private readonly DatabaseContext _context;
        public EfFreeBarberDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<FreeBarberDetailDto?> GetByFreeBarberPanel(Guid userId)
        {
            var dto = await _context.FreeBarbers
                .Where(bs => bs.FreeBarberUserId == userId)
                .Select(bs => new FreeBarberDetailDto
                {
                    Id = bs.Id,
                    FullName = bs.FullName,
                    FreeBarberImageUrl = bs.FreeBarberImageUrl,
                    Type = bs.Type,
                    Address = bs.Address,
                    FavoriteCount = _context.Favorites.Count(f => f.FavoritedToId == bs.FreeBarberUserId),
                    Rating = _context.Ratings.Where(r => r.TargetId == bs.FreeBarberUserId)
                                             .Average(r => (double?)r.Score) ?? 0,
                    ServiceOfferings = _context.ServiceOfferings
                        .Where(o => o.OwnerId == bs.Id)
                        .Select(o => new ServiceOfferingListDto { Id = o.Id, ServiceName = o.ServiceName, Price = o.Price })
                        .ToList(),
                    WorkingHours = _context.WorkingHours
                        .Where(wh => wh.OwnerId == bs.Id)
                        .Select(wh => new WorkingHourDto
                        {
                            Id = wh.Id,
                            DayOfWeek = wh.DayOfWeek,
                            StartTime = wh.StartTime,
                            EndTime = wh.EndTime,
                            IsClosed = wh.IsClosed
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (dto is null) return null;

            var tzId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? "Turkey Standard Time" : "Europe/Istanbul";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var nowTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            int today = (int)nowTr.DayOfWeek;
            var now = nowTr.TimeOfDay;

            var todays = dto.WorkingHours.Where(wh => (int)wh.DayOfWeek == today && !wh.IsClosed);
            dto.IsAvailable = todays.Any(wh =>
                (wh.StartTime <= wh.EndTime && now >= wh.StartTime && now <= wh.EndTime) ||
                (wh.StartTime > wh.EndTime && (now >= wh.StartTime || now <= wh.EndTime))
            );

            return dto;
        }


        public async Task<FreeBarberDetailDto?> GetByIdWithStatsAsync(Guid id)
        {
            var dto = await _context.FreeBarbers
                .Where(bs => bs.Id == id)
                .Select(bs => new FreeBarberDetailDto
                {
                    Id = bs.Id,
                    FullName = bs.FullName,
                    FreeBarberImageUrl = bs.FreeBarberImageUrl,
                    Type = bs.Type,
                    Address = bs.Address,
                    FavoriteCount = _context.Favorites.Count(f => f.FavoritedToId == bs.FreeBarberUserId),
                    Rating = _context.Ratings
                        .Where(r => r.TargetId == bs.FreeBarberUserId)
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

        public async Task<List<FreeBarberListDto>> GetNearbyFreeBarberWithStatsAsync(double userLat, double userLng, double maxDistanceKm = 1)
        {
            const double EarthRadiusKm = 6371;
            var userLatStr = userLat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var userLngStr = userLng.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var freeBarbers = await _context.Set<FreeBarberListDto>()
           .FromSqlInterpolated($@"
        SELECT
            bs.Id,
            bs.FullName,
            bs.FreeBarberImageUrl,
            bs.Type,
                            
            ISNULL(favCounts.FavoriteCount, 0)      AS FavoriteCount,
            ISNULL(ratingStats.AvgRating, 0)        AS Rating,
            ISNULL(ratingStats.CommentCount, 0)     AS ReviewCount,
            {EarthRadiusKm} * ACOS(
                 COS(RADIANS({userLat})) * COS(RADIANS(a.Latitude)) *
                 COS(RADIANS(a.Longitude) - RADIANS({userLng})) +
                 SIN(RADIANS({userLat})) * SIN(RADIANS(a.Latitude))
            ) AS DistanceKm
        FROM dbo.FreeBarbers bs
        INNER JOIN dbo.AddressInfos a  ON a.Id = bs.AddressId
        LEFT  JOIN (
            SELECT FavoritedToId, COUNT(*) AS FavoriteCount
            FROM dbo.Favorites
            GROUP BY FavoritedToId
        ) favCounts ON favCounts.FavoritedToId = bs.FreeBarberUserId
        LEFT  JOIN (
            SELECT TargetId,
                   AVG(CAST(Score AS FLOAT)) AS AvgRating,
                   COUNT(Comment)            AS CommentCount
            FROM dbo.Ratings
            GROUP BY TargetId
        ) ratingStats ON ratingStats.TargetId = bs.FreeBarberUserId
        WHERE {EarthRadiusKm} * ACOS(
                 COS(RADIANS({userLat})) * COS(RADIANS(a.Latitude)) *
                 COS(RADIANS(a.Longitude) - RADIANS({userLng})) +
                 SIN(RADIANS({userLat})) * SIN(RADIANS(a.Latitude))
              ) <= {maxDistanceKm}")
           .AsNoTracking()
           .ToListAsync();

            var ids = freeBarbers.Select(f => f.Id).ToList();
            var offerings = await _context.ServiceOfferings
                                 .Where(o => ids.Contains(o.OwnerId))
                                 .Select(o => new
                                 {
                                     o.OwnerId,
                                     DTO = new ServiceOfferingListDto
                                     {
                                         Id = o.Id,
                                         Price = o.Price,
                                         ServiceName = o.ServiceName
                                     }
                                 })
                                 .ToListAsync();
            foreach (var b in freeBarbers)
                b.ServiceOfferings = offerings.Where(x => x.OwnerId == b.Id)
                                              .Select(x => x.DTO)
                                              .ToList();

            var now = DateTime.Now;
            var dayOfWeek = (int)now.DayOfWeek;
            var currentTime = now.TimeOfDay;
            var workingHours = await _context.WorkingHours
            .Where(wh => ids.Contains(wh.OwnerId))
            .AsNoTracking()
            .ToListAsync();
            foreach (var freeBarber in freeBarbers)
            {
                var todayWorkingHours = workingHours
                    .Where(wh => wh.OwnerId == freeBarber.Id && (int)wh.DayOfWeek == dayOfWeek)
                    .ToList();
                if (!todayWorkingHours.Any() || todayWorkingHours.All(wh => wh.IsClosed))
                {
                    freeBarber.IsAvailable = false;
                }
                else
                {
                    freeBarber.IsAvailable = todayWorkingHours.Any(wh =>
                        currentTime >= wh.StartTime && currentTime <= wh.EndTime);
                }

            }
            return freeBarbers;
        }


    }
}
