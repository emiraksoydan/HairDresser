using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IChatThreadDal : IEntityRepository<ChatThread>
    {
        /// <summary>
        /// Gets chat threads for a user (both appointment and favorite threads)
        /// Note: Title and Participants will be set in business layer
        /// </summary>
        Task<List<ChatThreadListItemDto>> GetThreadsForUserAsync(Guid userId, AppointmentStatus[] allowedStatuses);
        
        /// <summary>
        /// Gets favorite threads for a user (where user is either FavoriteFromUserId or FavoriteToUserId)
        /// </summary>
        Task<List<ChatThread>> GetFavoriteThreadsForUserAsync(Guid userId);
        
        /// <summary>
        /// Gets or creates a favorite thread between two users
        /// </summary>
        Task<ChatThread?> GetFavoriteThreadAsync(Guid fromUserId, Guid toUserId, Guid? storeId = null);
        
        /// <summary>
        /// Gets unread message count for a user (database-level sum for performance)
        /// Includes both appointment and favorite threads
        /// </summary>
        Task<int> GetUnreadMessageCountAsync(Guid userId);
    }
}
