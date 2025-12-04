using Api.Hubs;
using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.SignalR;

namespace Api.RealTime
{
    public class SignalRRealtimePublisher(IHubContext<AppHub> hub) : IRealTimePublisher
    {
        public Task PushNotificationAsync(Guid userId, NotificationDto dto) =>
            hub.Clients.Group($"user:{userId}").SendAsync("notification.received", dto);

        public Task PushChatMessageAsync(Guid userId, ChatMessageDto dto) =>
            hub.Clients.Group($"user:{userId}").SendAsync("chat.message", dto);

        public Task PushBadgeAsync(Guid userId, BadgeCountDto dto) =>
            hub.Clients.Group($"user:{userId}").SendAsync("badge.updated", dto);


        public Task PushChatThreadCreatedAsync(Guid userId, ChatThreadListItemDto dto) =>
            hub.Clients.Group($"user:{userId}").SendAsync("chat.threadCreated", dto);
    }
}
