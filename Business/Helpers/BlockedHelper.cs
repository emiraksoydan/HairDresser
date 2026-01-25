using DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Helpers
{
    /// <summary>
    /// Engelleme kontrolü için helper sınıf
    /// </summary>
    public class BlockedHelper
    {
        private readonly IBlockedDal _blockedDal;

        public BlockedHelper(IBlockedDal blockedDal)
        {
            _blockedDal = blockedDal;
        }

        /// <summary>
        /// Kullanıcının engellediği ve engellendiği tüm kullanıcı ID'lerini getirir
        /// </summary>
        public async Task<HashSet<Guid>> GetAllBlockedUserIdsAsync(Guid userId)
        {
            return await _blockedDal.GetBlockedUserIdsAsync(userId);
        }

        /// <summary>
        /// İki kullanıcı arasında engelleme var mı kontrol eder (çift yönlü)
        /// </summary>
        public async Task<bool> HasBlockBetweenAsync(Guid userId1, Guid userId2)
        {
            return await _blockedDal.HasAnyBlockBetweenAsync(userId1, userId2);
        }

        /// <summary>
        /// Belirli bir kullanıcı engellemiş mi kontrol eder (tek yönlü)
        /// </summary>
        public async Task<bool> IsBlockedByAsync(Guid blockerId, Guid blockedId)
        {
            return await _blockedDal.IsBlockedAsync(blockerId, blockedId);
        }

        /// <summary>
        /// Store listesini engellenmiş kullanıcılara göre filtreler
        /// </summary>
        public async Task<List<T>> FilterBlockedStoresAsync<T>(
            Guid? currentUserId,
            List<T> stores,
            Func<T, Guid> getOwnerUserId)
        {
            if (!currentUserId.HasValue || stores == null || stores.Count == 0)
                return stores ?? new List<T>();

            var blockedIds = await GetAllBlockedUserIdsAsync(currentUserId.Value);
            if (blockedIds.Count == 0)
                return stores;

            return stores.Where(s => !blockedIds.Contains(getOwnerUserId(s))).ToList();
        }

        /// <summary>
        /// FreeBarber listesini engellenmiş kullanıcılara göre filtreler
        /// </summary>
        public async Task<List<T>> FilterBlockedUsersAsync<T>(
            Guid? currentUserId,
            List<T> users,
            Func<T, Guid> getUserId)
        {
            if (!currentUserId.HasValue || users == null || users.Count == 0)
                return users ?? new List<T>();

            var blockedIds = await GetAllBlockedUserIdsAsync(currentUserId.Value);
            if (blockedIds.Count == 0)
                return users;

            return users.Where(u => !blockedIds.Contains(getUserId(u))).ToList();
        }
    }
}
