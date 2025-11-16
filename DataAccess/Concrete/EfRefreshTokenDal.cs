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
        public async Task Add(RefreshToken token)
        {
            _context.Set<RefreshToken>().Add(token);
            await _context.SaveChangesAsync();
        }
        public async Task Update(RefreshToken token)
        {
            _context.Set<RefreshToken>().Update(token);
            await _context.SaveChangesAsync();
        }

        public async Task<List<RefreshToken>> GetActiveByUser(Guid userId) =>
           await _context.Set<RefreshToken>().Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow).ToListAsync();
        public async Task<RefreshToken?> GetByFingerprintAsync(string fingerprint) =>
       await _context.Set<RefreshToken>()
           .FirstOrDefaultAsync(r => r.Fingerprint == fingerprint);

        public async Task RevokeFamilyAsync(Guid familyId, string reason, string? ip)
        {
            var now = DateTime.UtcNow;
            await _context.Set<RefreshToken>()
                .Where(r => r.FamilyId == familyId &&
                            r.RevokedAt == null &&
                            r.ExpiresAt > now)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(r => r.RevokedAt, now)
                    .SetProperty(r => r.RevokedByIp, ip)
                );
        }
    }

}
