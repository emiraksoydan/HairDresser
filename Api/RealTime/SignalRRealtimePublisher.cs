using Api.Hubs;
using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.SignalR;
using System;

namespace Api.RealTime
{
    public class SignalRRealtimePublisher(IHubContext<AppHub> hub) : IRealTimePublisher
    {
        public async Task PushNotificationAsync(Guid userId, NotificationDto dto)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("notification.received", dto);
            }
            catch (Exception)
            {
                // Log error but don't throw - notification is already in DB
                // Consider adding ILogger<T> for proper logging
            }
        }

        public async Task PushChatMessageAsync(Guid userId, ChatMessageDto dto)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("chat.message", dto);
            }
            catch (Exception)
            {
                // Log error but don't throw - message is already in DB
            }
        }

        public async Task PushBadgeAsync(Guid userId, BadgeCountDto dto)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("badge.updated", dto);
            }
            catch (Exception)
            {
                // Log error but don't throw - badge count can be refetched
            }
        }

        public async Task PushChatThreadCreatedAsync(Guid userId, ChatThreadListItemDto dto)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("chat.threadCreated", dto);
            }
            catch (Exception)
            {
                // Log error but don't throw - thread is already in DB
            }
        }

        public async Task PushChatThreadUpdatedAsync(Guid userId, ChatThreadListItemDto dto)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("chat.threadUpdated", dto);
            }
            catch (Exception)
            {
                // Log error but don't throw - thread update can be refetched
            }
        }

        public async Task PushChatThreadRemovedAsync(Guid userId, Guid threadId)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("chat.threadRemoved", threadId);
            }
            catch (Exception)
            {
                // Log error but don't throw - thread removal can be refetched
            }
        }

        public async Task PushChatTypingAsync(Guid userId, Guid threadId, Guid typingUserId, string typingUserName, bool isTyping)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("chat.typing", new
                {
                    threadId,
                    typingUserId,
                    typingUserName,
                    isTyping
                });
            }
            catch (Exception)
            {
                // Log error but don't throw - typing indicator is non-critical
            }
        }
    }
}
