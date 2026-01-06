using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfUserFcmTokenDal : EfEntityRepositoryBase<UserFcmToken, DatabaseContext>, IUserFcmTokenDal
    {
        private readonly DatabaseContext _context;

        public EfUserFcmTokenDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<UserFcmToken>> GetActiveTokensByUserIdAsync(Guid userId)
        {
            return await _context.Set<UserFcmToken>()
                .Where(x => x.UserId == userId && x.IsActive)
                .OrderByDescending(x => x.UpdatedAt) // Most recently used tokens first
                .ToListAsync();
        }

        public async Task<UserFcmToken?> GetByTokenAsync(string fcmToken)
        {
            return await _context.Set<UserFcmToken>()
                .FirstOrDefaultAsync(x => x.FcmToken == fcmToken);
        }

        public async Task DeactivateTokenAsync(string fcmToken)
        {
            var token = await _context.Set<UserFcmToken>()
                .FirstOrDefaultAsync(x => x.FcmToken == fcmToken);
            if (token != null)
            {
                token.IsActive = false;
                token.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeactivateAllUserTokensAsync(Guid userId)
        {
            var tokens = await _context.Set<UserFcmToken>()
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync();
            
            if (tokens.Count == 0) return;
            
            foreach (var token in tokens)
            {
                token.IsActive = false;
                token.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }
    }
}

