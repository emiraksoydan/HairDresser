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
        /// Gets chat threads for a user with appointment status filtering
        /// Note: Title is set to empty string - should be set in business layer
        /// </summary>
        Task<List<ChatThreadListItemDto>> GetThreadsForUserAsync(Guid userId, AppointmentStatus[] allowedStatuses);
        
        /// <summary>
        /// Gets unread message count for a user (database-level sum for performance)
        /// </summary>
        Task<int> GetUnreadMessageCountAsync(Guid userId);
    }
}
