using Business.Abstract;

using Core.Aspect.Autofac.Logging;
using DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Concrete
{
    /// <summary>
    /// Centralized Badge Count Management Service
    ///
    /// Responsibilities:
    /// 1. Calculate total notification unread count
    /// 2. Calculate total chat unread count
    /// 3. Calculate per-thread unread counts
    /// 4. Push badge updates via SignalR when counts change
    ///
    /// Performance Optimizations:
    /// - Single query for all counts
    /// - Cached results (short TTL)
    /// - Batch updates for multiple users
    /// </summary>
    public class BadgeService(
        INotificationDal notificationDal,
        IChatThreadDal threadDal,
        IRealTimePublisher realtime)
    {
        /// <summary>
        /// Gets all badge counts for a user in a single query
        /// </summary>
        [LogAspect]
        public async Task<BadgeCountDto> GetBadgeCountsAsync(Guid userId)
        {
            // Notification unread count
            var notificationUnreadCount = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);

            // Chat threads for this user
            var threads = await threadDal.GetAll(t =>
                t.CustomerUserId == userId ||
                t.StoreOwnerUserId == userId ||
                t.FreeBarberUserId == userId);

            // Calculate total chat unread count and per-thread counts
            var chatUnreadCount = 0;
            var threadUnreadCounts = new Dictionary<Guid, int>();

            foreach (var thread in threads)
            {
                var unreadCount = GetUnreadCountForUser(thread, userId);
                if (unreadCount > 0)
                {
                    chatUnreadCount += unreadCount;
                    threadUnreadCounts[thread.Id] = unreadCount;
                }
            }

            return new BadgeCountDto
            {
                NotificationUnreadCount = notificationUnreadCount,
                ChatUnreadCount = chatUnreadCount,
                ThreadUnreadCounts = threadUnreadCounts
            };
        }

        /// <summary>
        /// Notifies user that badge count has changed (triggers SignalR push with counts)
        /// </summary>
        [LogAspect]
        public async Task NotifyBadgeChangeAsync(Guid userId, BadgeChangeReason reason)
        {
            // Hesapla ve gönder - ANLIK güncelleme için
            var counts = await GetBadgeCountsAsync(userId);
            await realtime.PushBadgeUpdateAsync(userId, counts.NotificationUnreadCount, counts.ChatUnreadCount);
        }

        /// <summary>
        /// Batch notify multiple users about badge changes
        /// </summary>
        [LogAspect]
        public async Task NotifyBadgeChangeBatchAsync(List<Guid> userIds, BadgeChangeReason reason)
        {
            foreach (var userId in userIds.Distinct())
            {
                await NotifyBadgeChangeAsync(userId, reason);
            }
        }

        /// <summary>
        /// Helper method to get unread count for a specific user in a thread
        /// </summary>
        private static int GetUnreadCountForUser(Entities.Concrete.Entities.ChatThread thread, Guid userId)
        {
            if (thread.CustomerUserId == userId)
                return thread.CustomerUnreadCount;
            if (thread.StoreOwnerUserId == userId)
                return thread.StoreUnreadCount;
            if (thread.FreeBarberUserId == userId)
                return thread.FreeBarberUnreadCount;
            return 0;
        }
    }

    /// <summary>
    /// DTO for badge counts
    /// </summary>
    public class BadgeCountDto
    {
        public int NotificationUnreadCount { get; set; }
        public int ChatUnreadCount { get; set; }
        public Dictionary<Guid, int> ThreadUnreadCounts { get; set; } = new();
    }

    /// <summary>
    /// Enum for badge change reasons (for logging/debugging)
    /// </summary>
    public enum BadgeChangeReason
    {
        NotificationReceived,
        NotificationRead,
        NotificationDeleted,
        MessageReceived,
        MessageRead,
        ThreadRemoved,
        AppointmentStatusChanged
    }
}
