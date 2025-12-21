using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    /// <summary>
    /// Transaction commit sonrası badge update'leri yönetmek için service
    /// </summary>
    public interface IBadgeUpdateService
    {
        /// <summary>
        /// Transaction commit sonrası badge update yapılacak kullanıcı ID'sini kaydet
        /// </summary>
        void ScheduleBadgeUpdate(Guid userId);

        /// <summary>
        /// Kaydedilen tüm badge update'leri çalıştır (transaction commit sonrası çağrılmalı)
        /// </summary>
        Task ProcessScheduledBadgeUpdatesAsync();
    }
}

