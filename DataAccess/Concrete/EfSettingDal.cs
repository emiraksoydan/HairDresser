using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSettingDal : EfEntityRepositoryBase<Setting, DatabaseContext>, ISettingDal
    {
        private readonly DatabaseContext _context;

        public EfSettingDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Setting?> GetByUserIdAsync(Guid userId)
        {
            return await _context.Settings
                .FirstOrDefaultAsync(s => s.UserId == userId);
        }
    }
}

