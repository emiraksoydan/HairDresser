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
    public class EfBlockedDal : EfEntityRepositoryBase<Blocked, DatabaseContext>, IBlockedDal
    {
        private readonly DatabaseContext _context;

        public EfBlockedDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<HashSet<Guid>> GetBlockedUserIdsAsync(Guid userId)
        {
            // Kullanıcının engellediği + kullanıcıyı engelleyen ID'ler
            var blockedByMe = await _context.Blockeds
                .Where(b => b.BlockedFromUserId == userId && !b.IsDeleted)
                .Select(b => b.BlockedToUserId)
                .ToListAsync();

            var blockedMe = await _context.Blockeds
                .Where(b => b.BlockedToUserId == userId && !b.IsDeleted)
                .Select(b => b.BlockedFromUserId)
                .ToListAsync();

            var allBlocked = new HashSet<Guid>(blockedByMe);
            foreach (var id in blockedMe)
            {
                allBlocked.Add(id);
            }

            return allBlocked;
        }

        public async Task<bool> IsBlockedAsync(Guid blockedFromUserId, Guid blockedToUserId)
        {
            return await _context.Blockeds
                .AnyAsync(b =>
                    b.BlockedFromUserId == blockedFromUserId &&
                    b.BlockedToUserId == blockedToUserId &&
                    !b.IsDeleted);
        }

        public async Task<bool> HasAnyBlockBetweenAsync(Guid userId1, Guid userId2)
        {
            return await _context.Blockeds
                .AnyAsync(b =>
                    !b.IsDeleted &&
                    ((b.BlockedFromUserId == userId1 && b.BlockedToUserId == userId2) ||
                    (b.BlockedFromUserId == userId2 && b.BlockedToUserId == userId1)));
        }

        public async Task<List<Blocked>> GetBlockedByUserAsync(Guid userId)
        {
            return await _context.Blockeds
                .Where(b => b.BlockedFromUserId == userId && !b.IsDeleted)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UnblockAsync(Guid blockedFromUserId, Guid blockedToUserId)
        {
            var blocked = await _context.Blockeds
                .FirstOrDefaultAsync(b =>
                    b.BlockedFromUserId == blockedFromUserId &&
                    b.BlockedToUserId == blockedToUserId &&
                    !b.IsDeleted);

            if (blocked == null)
                return false;

            // Soft delete
            blocked.IsDeleted = true;
            blocked.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
