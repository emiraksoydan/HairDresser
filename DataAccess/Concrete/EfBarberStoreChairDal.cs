using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfBarberStoreChairDal : EfEntityRepositoryBase<BarberChair, DatabaseContext>, IBarberStoreChairDal
    {
        private readonly DatabaseContext _context;
        public EfBarberStoreChairDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<BarberChair>> GetAllWithManualBarberAsync(Expression<Func<BarberChair, bool>> filter)
        {
            return await _context.BarberChairs
                .Include(x => x.ManualBarber)
                .Where(filter)
                .ToListAsync();
        }

    
    }
}
