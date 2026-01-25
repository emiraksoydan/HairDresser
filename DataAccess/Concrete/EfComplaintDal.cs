using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfComplaintDal : EfEntityRepositoryBase<Complaint, DatabaseContext>, IComplaintDal
    {
        private readonly DatabaseContext _context;

        public EfComplaintDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<bool> ExistsAsync(Guid complaintFromUserId, Guid complaintToUserId, Guid? appointmentId)
        {
            return await _context.Complaints
                .AnyAsync(c =>
                    c.ComplaintFromUserId == complaintFromUserId &&
                    c.ComplaintToUserId == complaintToUserId &&
                    c.AppointmentId == appointmentId &&
                    !c.IsDeleted);
        }

        public async Task<List<Complaint>> GetByUserAsync(Guid userId)
        {
            return await _context.Complaints
                .Where(c => c.ComplaintFromUserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}
