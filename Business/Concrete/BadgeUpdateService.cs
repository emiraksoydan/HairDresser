using Business.Abstract;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Concrete
{
    /// <summary>
    /// Transaction commit sonrası badge update'leri yönetir
    /// </summary>
    public class BadgeUpdateService : IBadgeUpdateService
    {
        private readonly IBadgeService _badgeService;
        private readonly IRealTimePublisher _realtime;
        private readonly HashSet<Guid> _pendingUserIds = new HashSet<Guid>();

        public BadgeUpdateService(IBadgeService badgeService, IRealTimePublisher realtime)
        {
            _badgeService = badgeService;
            _realtime = realtime;
        }

        /// <summary>
        /// Transaction commit sonrası badge update yapılacak kullanıcı ID'sini kaydet
        /// </summary>
        public void ScheduleBadgeUpdate(Guid userId)
        {
            lock (_pendingUserIds)
            {
                _pendingUserIds.Add(userId);
            }
        }

        /// <summary>
        /// Kaydedilen tüm badge update'leri çalıştır (transaction commit sonrası çağrılmalı)
        /// </summary>
        public async Task ProcessScheduledBadgeUpdatesAsync()
        {
            HashSet<Guid> userIdsToUpdate;
            
            // Thread-safe: Pending listesini kopyala ve temizle
            lock (_pendingUserIds)
            {
                if (_pendingUserIds.Count == 0)
                    return;
                    
                userIdsToUpdate = new HashSet<Guid>(_pendingUserIds);
                _pendingUserIds.Clear();
            }

            // Her kullanıcı için badge update yap
            foreach (var userId in userIdsToUpdate)
            {
                try
                {
                    var badges = await _badgeService.GetCountsAsync(userId);
                    if (badges.Success)
                    {
                        await _realtime.PushBadgeAsync(userId, badges.Data);
                    }
                }
                catch
                {
                    // Badge güncellemesi başarısız olursa devam et, kritik değil
                    // Loglama eklenebilir
                }
            }
        }
    }
}

