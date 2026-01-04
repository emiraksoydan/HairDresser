using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfChatThreadDal : EfEntityRepositoryBase<ChatThread, DatabaseContext>, IChatThreadDal
    {
        public EfChatThreadDal(DatabaseContext context) : base(context) { }

        public async Task<List<ChatThreadListItemDto>> GetThreadsForUserAsync(Guid userId, AppointmentStatus[] allowedStatuses)
        {
            // Randevu thread'leri (AppointmentId != null)
            var appointmentThreads = await Context.ChatThreads.AsNoTracking()
                .Where(t => t.AppointmentId.HasValue &&
                           (t.CustomerUserId == userId || t.StoreOwnerUserId == userId || t.FreeBarberUserId == userId))
                .Join(Context.Appointments.AsNoTracking()
                      .Where(a => (a.CustomerUserId == userId && !a.IsDeletedByCustomerUserId) ||
                                  (a.BarberStoreUserId == userId && !a.IsDeletedByBarberStoreUserId) ||
                                  (a.FreeBarberUserId == userId && !a.IsDeletedByFreeBarberUserId)),
                      t => t.AppointmentId!.Value,
                      a => a.Id,
                      (t, a) => new { t, a })
                .Where(x => allowedStatuses.Contains(x.a.Status))
                .Select(x => new ChatThreadListItemDto
                {
                    ThreadId = x.t.Id,
                    AppointmentId = x.a.Id,
                    Status = x.a.Status,
                    IsFavoriteThread = false,
                    Title = string.Empty, // Title will be set in business layer
                    LastMessagePreview = x.t.LastMessagePreview,
                    LastMessageAt = x.t.LastMessageAt,
                    UnreadCount = x.t.CustomerUserId == userId ? x.t.CustomerUnreadCount :
                                  x.t.StoreOwnerUserId == userId ? x.t.StoreUnreadCount :
                                  x.t.FreeBarberUserId == userId ? x.t.FreeBarberUnreadCount : 0
                })
                .ToListAsync();

            // Favori thread'leri (AppointmentId == null)
            var favoriteThreads = await Context.ChatThreads.AsNoTracking()
                .Where(t => !t.AppointmentId.HasValue &&
                           t.FavoriteFromUserId.HasValue &&
                           t.FavoriteToUserId.HasValue &&
                           (t.FavoriteFromUserId == userId || t.FavoriteToUserId == userId))
                .Select(t => new ChatThreadListItemDto
                {
                    ThreadId = t.Id,
                    AppointmentId = null,
                    Status = null,
                    IsFavoriteThread = true,
                    Title = string.Empty, // Title will be set in business layer
                    LastMessagePreview = t.LastMessagePreview,
                    LastMessageAt = t.LastMessageAt,
                    UnreadCount = (t.FavoriteFromUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                                  (t.FavoriteFromUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                                  (t.FavoriteFromUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount :
                                  (t.FavoriteToUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                                  (t.FavoriteToUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                                  (t.FavoriteToUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount : 0
                })
                .ToListAsync();

            var allThreads = appointmentThreads.Concat(favoriteThreads)
                .OrderByDescending(t => t.LastMessageAt ?? DateTime.MinValue)
                .ToList();

            return allThreads;
        }

        public async Task<List<ChatThread>> GetFavoriteThreadsForUserAsync(Guid userId)
        {
            return await Context.ChatThreads.AsNoTracking()
                .Where(t => !t.AppointmentId.HasValue &&
                           t.FavoriteFromUserId.HasValue &&
                           t.FavoriteToUserId.HasValue &&
                           (t.FavoriteFromUserId == userId || t.FavoriteToUserId == userId))
                .ToListAsync();
        }

        public async Task<ChatThread?> GetFavoriteThreadAsync(Guid fromUserId, Guid toUserId, Guid? storeId = null)
        {
            // REVIZE: StoreId parametresi artık kullanılmıyor - User ID bazlı tek thread olmalı
            // Her iki yönü de kontrol et (from->to veya to->from)
            // StoreId null olmalı (User ID bazlı thread)
            var thread = await Context.ChatThreads
                .FirstOrDefaultAsync(t => !t.AppointmentId.HasValue &&
                                         ((t.FavoriteFromUserId == fromUserId && t.FavoriteToUserId == toUserId) ||
                                          (t.FavoriteFromUserId == toUserId && t.FavoriteToUserId == fromUserId)) &&
                                         t.StoreId == null);
            return thread;
        }

        /// <summary>
        /// Gets unread message count for a user (database-level sum for performance)
        /// Includes both appointment and favorite threads
        /// </summary>
        public async Task<int> GetUnreadMessageCountAsync(Guid userId)
        {
            // Randevu thread'leri için
            var appointmentThreadsCount = await Context.ChatThreads
                .Where(t => t.AppointmentId.HasValue &&
                           ((t.CustomerUserId == userId && !t.IsDeletedByCustomerUserId) ||
                            (t.StoreOwnerUserId == userId && !t.IsDeletedByStoreOwnerUserId) ||
                            (t.FreeBarberUserId == userId && !t.IsDeletedByFreeBarberUserId)))
                .SumAsync(t =>
                    t.CustomerUserId == userId ? t.CustomerUnreadCount :
                    t.StoreOwnerUserId == userId ? t.StoreUnreadCount :
                    t.FreeBarberUserId == userId ? t.FreeBarberUnreadCount : 0);

            // Favori thread'leri için
            var favoriteThreadsCount = await Context.ChatThreads
                .Where(t => !t.AppointmentId.HasValue &&
                           t.FavoriteFromUserId.HasValue &&
                           t.FavoriteToUserId.HasValue &&
                           (t.FavoriteFromUserId == userId || t.FavoriteToUserId == userId))
                .SumAsync(t =>
                    (t.FavoriteFromUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                    (t.FavoriteFromUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                    (t.FavoriteFromUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount :
                    (t.FavoriteToUserId == userId && t.CustomerUserId == userId) ? t.CustomerUnreadCount :
                    (t.FavoriteToUserId == userId && t.StoreOwnerUserId == userId) ? t.StoreUnreadCount :
                    (t.FavoriteToUserId == userId && t.FreeBarberUserId == userId) ? t.FreeBarberUnreadCount : 0);

            return appointmentThreadsCount + favoriteThreadsCount;
        }
    }
}
