using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IRealTimePublisher
    {
        Task PushNotificationAsync(Guid userId, NotificationDto dto);
        Task PushChatMessageAsync(Guid userId, ChatMessageDto dto);
        Task PushBadgeAsync(Guid userId, BadgeCountDto dto);
        Task PushChatThreadCreatedAsync(Guid userId, ChatThreadListItemDto dto);
        Task PushChatThreadUpdatedAsync(Guid userId, ChatThreadListItemDto dto);
        Task PushChatThreadRemovedAsync(Guid userId, Guid threadId);
        Task PushChatTypingAsync(Guid userId, Guid threadId, Guid typingUserId, string typingUserName, bool isTyping);

    }
}
