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
            var nowUtc = DateTime.Now;

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
                    Rating = _context.Ratings
                                     .Where(r => r.TargetId == bs.FreeBarberUserId)
                                     .Average(r => (double?)r.Score) ?? 0,
                    ServiceOfferings = _context.ServiceOfferings
                        .Where(o => o.OwnerId == bs.Id)
                        .Select(o => new ServiceOfferingListDto { Id = o.Id, ServiceName = o.ServiceName, Price = o.Price })
                        .ToList(),
                    IsAvailable = !_context.Appointments.Any(a =>
                        (a.PerformerUserId == bs.Id || a.BookedByUserId == bs.Id) &&
                        a.Status == AppointmentStatus.Approved &&
                        a.StartUtc <= nowUtc &&
                        a.EndTime >= nowUtc)
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            return dto;
        }


        public async Task<FreeBarberDetailDto?> GetByIdWithStatsAsync(Guid id)
        {
            var nowUtc = DateTime.Now;
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
                    IsAvailable = !_context.Appointments.Any(a =>
                        (a.PerformerUserId == bs.Id || a.BookedByUserId == bs.Id) &&
                        a.Status == AppointmentStatus.Approved &&
                        a.StartUtc <= nowUtc &&
                        a.EndTime > nowUtc)
                })
                .AsNoTracking()
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
            var nowUtc = DateTime.Now;
            var busyPairs = await _context.Appointments
                .Where(a =>
                    (ids.Contains(a.PerformerUserId ?? Guid.Empty) || ids.Contains(a.BookedByUserId)) && 
                    a.Status == AppointmentStatus.Approved &&
                    a.StartUtc <= nowUtc &&
                    a.EndTime > nowUtc)
                .Select(a => new { a.PerformerUserId, a.BookedByUserId })
                .AsNoTracking()
                .ToListAsync();
            var busySet = new HashSet<Guid>(
                busyPairs.SelectMany(p => new[] { p.PerformerUserId ?? Guid.Empty, p.BookedByUserId }) 
            );
            foreach (var b in freeBarbers)
            {
                b.IsAvailable = !busySet.Contains(b.Id);
            }
            return freeBarbers;
        }
    }
}
