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
    public class EfRequestDal : EfEntityRepositoryBase<Request, DatabaseContext>, IRequestDal
    {
        private readonly DatabaseContext _context;

        public EfRequestDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<Request>> GetByUserAsync(Guid userId)
        {
            return await _context.Requests
                .Where(r => r.RequestFromUserId == userId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}
