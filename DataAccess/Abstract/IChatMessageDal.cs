using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IChatMessageDal : IEntityRepository<ChatMessage>
    {
        Task<List<ChatMessageItemDto>> GetMessagesForAppointmentAsync(Guid appointmentId, DateTime? beforeUtc);
        
        /// <summary>
        /// Gets messages by thread ID (works for both appointment and favorite threads)
        /// </summary>
        Task<List<ChatMessageItemDto>> GetMessagesByThreadIdAsync(Guid threadId, DateTime? beforeUtc);
    }
}
