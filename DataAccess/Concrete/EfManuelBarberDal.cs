using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfManuelBarberDal : EfEntityRepositoryBase<ManuelBarber, DatabaseContext>, IManuelBarberDal
    {
        private readonly DatabaseContext _context;

        public EfManuelBarberDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<ManuelBarberRatingDto>> GetManualBarberRatingsAsync(List<Guid> barberIds)
        {
            var ratings = await(from mb in _context.ManuelBarbers
                                where barberIds.Contains(mb.Id)
                                join r in _context.Ratings on mb.Id equals r.TargetId into ratingGroup
                                from subRating in ratingGroup.DefaultIfEmpty()
                                group subRating by new { mb.Id, mb.FullName } into g
                                select new ManuelBarberRatingDto
                                {
                                    BarberId = g.Key.Id,
                                    BarberName = g.Key.FullName,
                                    Rating = g.Average(x => x != null ? x.Score : 0)
                                }).ToListAsync();

            return ratings;
        }
    }
}
