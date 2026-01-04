using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class ChatThread : IEntity
    {
        public Guid Id { get; set; }
        
        // Randevu thread'i için: AppointmentId dolu, FavoriteFromUserId/FavoriteToUserId null
        // Favori thread için: AppointmentId null, FavoriteFromUserId/FavoriteToUserId dolu
        public Guid? AppointmentId { get; set; }
        
        // Favori thread için: hangi kullanıcı favoriye ekledi, hangi kullanıcı favoriye eklendi
        public Guid? FavoriteFromUserId { get; set; }
        public Guid? FavoriteToUserId { get; set; }
        
        // Store bazlı favori thread'leri için: StoreId (her dükkan için ayrı thread)
        // Store favori thread'inde: StoreId dolu olmalı, FavoriteFromUserId/FavoriteToUserId user ID'ler
        // Diğer favori thread'lerinde (Customer-FreeBarber): StoreId null
        public Guid? StoreId { get; set; }

        public Guid? CustomerUserId { get; set; }
        public Guid? StoreOwnerUserId { get; set; }
        public Guid? FreeBarberUserId { get; set; }

        public int CustomerUnreadCount { get; set; }
        public int StoreUnreadCount { get; set; }
        public int FreeBarberUnreadCount { get; set; }

        public DateTime? LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Soft Delete: Her kullanıcı tipi için ayrı soft delete alanı
        public bool IsDeletedByCustomerUserId { get; set; } = false;
        public bool IsDeletedByStoreOwnerUserId { get; set; } = false;
        public bool IsDeletedByFreeBarberUserId { get; set; } = false;
    }
}
