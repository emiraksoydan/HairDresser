using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IChatService
    {
        Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text);
        Task<IDataResult<bool>> MarkThreadReadAsync(Guid userId, Guid appointmentId);

        Task<IDataResult<List<ChatThreadListItemDto>>> GetThreadsAsync(Guid userId);
        Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesAsync(Guid userId, Guid appointmentId, DateTime? beforeUtc);

        Task<IDataResult<int>> GetUnreadTotalAsync(Guid userId);
    }
}
