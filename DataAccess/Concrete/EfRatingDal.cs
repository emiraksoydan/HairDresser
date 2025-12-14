using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfRatingDal : EfEntityRepositoryBase<Rating, DatabaseContext>, IRatingDal
    {
        private readonly DatabaseContext _context;

        public EfRatingDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Rating> GetByAppointmentAndTargetAsync(Guid appointmentId, Guid targetId, Guid ratedFromId)
        {
            return await _context.Ratings
                .FirstOrDefaultAsync(r => 
                    r.AppointmentId == appointmentId && 
                    r.TargetId == targetId && 
                    r.RatedFromId == ratedFromId);
        }

        public async Task<bool> ExistsAsync(Guid appointmentId, Guid targetId, Guid ratedFromId)
        {
            return await _context.Ratings
                .AnyAsync(r => 
                    r.AppointmentId == appointmentId && 
                    r.TargetId == targetId && 
                    r.RatedFromId == ratedFromId);
        }
    }
}
