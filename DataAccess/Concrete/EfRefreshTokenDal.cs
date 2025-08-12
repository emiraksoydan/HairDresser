using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfRefreshTokenDal : EfEntityRepositoryBase<RefreshToken, DatabaseContext>, IRefreshTokenDal
    {
        private readonly DatabaseContext _context;
        public EfRefreshTokenDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }
        public async Task Revoke(Guid id)
        {
            var token = await _context.RefreshTokens.FindAsync(id);
            if (token is not null && token.IsActive)
            {
                token.Revoked = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

}
