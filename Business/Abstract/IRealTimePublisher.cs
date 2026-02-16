using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IRealTimePublisher
    {
        Task PushNotificationAsync(Guid userId, NotificationDto dto);
        Task PushNotificationSilentUpdateAsync(Guid userId, NotificationDto dto);
        Task PushChatMessageAsync(Guid userId, ChatMessageDto dto);
        Task PushChatMessageRemovedAsync(Guid userId, Guid threadId, Guid messageId);
        Task PushChatThreadCreatedAsync(Guid userId, ChatThreadListItemDto dto);
        Task PushChatThreadUpdatedAsync(Guid userId, ChatThreadListItemDto dto);
        Task PushChatThreadRemovedAsync(Guid userId, Guid threadId);
        Task PushChatTypingAsync(Guid userId, Guid threadId, Guid typingUserId, string typingUserName, bool isTyping);
        Task PushChatMessagesReadAsync(Guid userId, Guid threadId, Guid readerUserId, List<Guid> messageIds);
        Task PushAppointmentUpdatedAsync(Guid userId, Entities.Concrete.Dto.AppointmentGetDto appointment);
        Task PushBadgeUpdateAsync(Guid userId, int? notificationUnreadCount = null, int? chatUnreadCount = null);
        Task PushImageUpdatedAsync(Guid userId, Guid imageId, string imageUrl);
        Task PushImageRemovedAsync(Guid userId, Guid imageId);
    }
}
