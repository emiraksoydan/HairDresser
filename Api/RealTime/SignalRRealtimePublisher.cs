using Api.Hubs;
using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Api.RealTime
{
    public class SignalRRealtimePublisher(IHubContext<AppHub> hub, ILogger<SignalRRealtimePublisher> logger) : IRealTimePublisher
    {
        public async Task PushNotificationAsync(Guid userId, NotificationDto dto)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("notification.received", dto);
            }
            catch (Exception ex)
            {
                // Log error with full details but don't throw - notification is already in DB
                logger.LogError(ex, "Failed to send notification.received to user {UserId} for notification {NotificationId}. Exception: {ExceptionMessage}, StackTrace: {StackTrace}",
                    userId, dto.Id, ex.Message, ex.StackTrace);
            }
        }

        public async Task PushNotificationSilentUpdateAsync(Guid userId, NotificationDto dto)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("notification.updated", dto);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send notification.updated to user {UserId} for notification {NotificationId}", userId, dto.Id);
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

        public async Task PushChatMessageRemovedAsync(Guid userId, Guid threadId, Guid messageId)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("chat.messageRemoved", new { threadId, messageId });
            }
            catch (Exception)
            {
                // Non-critical: client will not see the removal event but message is already deleted from DB
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

        public async Task PushChatMessagesReadAsync(Guid userId, Guid threadId, Guid readerUserId, List<Guid> messageIds)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("chat.messagesRead", new
                {
                    threadId,
                    readerUserId,
                    messageIds
                });
            }
            catch (Exception)
            {
                // Non-critical: tick display is cosmetic, read state is already in DB
            }
        }

        public async Task PushAppointmentUpdatedAsync(Guid userId, Entities.Concrete.Dto.AppointmentGetDto appointment)
        {
            try
            {
                await hub.Clients.Group($"user:{userId}").SendAsync("appointment.updated", appointment);
            }
            catch (Exception)
            {
                // Log error but don't throw - appointment update can be refetched
            }
        }

        public async Task PushBadgeUpdateAsync(Guid userId, int? notificationUnreadCount = null, int? chatUnreadCount = null)
        {
            try
            {
                // Count'lar varsa frontend'e direkt gönder (ANLIK güncelleme)
                // Yoksa sadece event gönder (frontend invalidate yapacak)
                if (notificationUnreadCount.HasValue || chatUnreadCount.HasValue)
                {
                    await hub.Clients.Group($"user:{userId}").SendAsync("badge.updated", new
                    {
                        notificationUnreadCount,
                        chatUnreadCount
                    });
                }
                else
                {
                    await hub.Clients.Group($"user:{userId}").SendAsync("badge.updated");
                }
            }
            catch (Exception)
            {
                // Log error but don't throw - non-critical
            }
        }

        public async Task PushImageUpdatedAsync(Guid userId, Guid imageId, string imageUrl)
        {
            try
            {
                // Push to ALL users - image görünürlüğü global (chat, notification, card'lar)
                await hub.Clients.All.SendAsync("image.updated", new
                {
                    userId,
                    imageId,
                    imageUrl,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send image.updated for user {UserId}, image {ImageId}", userId, imageId);
            }
        }

        public async Task PushImageRemovedAsync(Guid userId, Guid imageId)
        {
            try
            {
                await hub.Clients.All.SendAsync("image.removed", new
                {
                    userId,
                    imageId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send image.removed for user {UserId}, image {ImageId}", userId, imageId);
            }
        }
    }
}
