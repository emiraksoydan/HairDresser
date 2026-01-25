using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IBlockedDal : IEntityRepository<Blocked>
    {
        /// <summary>
        /// Kullanıcının engellediği veya tarafından engellendiği kullanıcı ID'lerini getirir
        /// </summary>
        Task<HashSet<Guid>> GetBlockedUserIdsAsync(Guid userId);

        /// <summary>
        /// Belirli bir kullanıcıyı engellemiş mi kontrol eder
        /// </summary>
        Task<bool> IsBlockedAsync(Guid blockedFromUserId, Guid blockedToUserId);

        /// <summary>
        /// İki kullanıcı arasında herhangi bir engelleme var mı (çift yönlü)
        /// </summary>
        Task<bool> HasAnyBlockBetweenAsync(Guid userId1, Guid userId2);

        /// <summary>
        /// Kullanıcının engellediği kişilerin listesi
        /// </summary>
        Task<List<Blocked>> GetBlockedByUserAsync(Guid userId);

        /// <summary>
        /// Engellemeyi kaldır
        /// </summary>
        Task<bool> UnblockAsync(Guid blockedFromUserId, Guid blockedToUserId);
    }
}
