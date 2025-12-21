using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class ChatThreadListItemDto : IDto
    {
        public Guid ThreadId { get; set; } // Her thread için unique ID (hem randevu hem favori için)
        
        // Randevu thread'i için dolu, favori thread için null
        public Guid? AppointmentId { get; set; }
        
        // Randevu thread'i için: AppointmentStatus, favori thread için null
        public AppointmentStatus? Status { get; set; }
        
        // Favori thread için: true, randevu thread'i için: false
        public bool IsFavoriteThread { get; set; }

        public string Title { get; set; } = default!; // UI'da göstereceğin başlık
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAt { get; set; }

        public int UnreadCount { get; set; }
        
        // Mevcut kullanıcının profil resmi (mesaj balonlarında göstermek için)
        public string? CurrentUserImageUrl { get; set; }
        
        // Thread'deki diğer kullanıcıların bilgileri (mesajlaştığı kişiler)
        public List<ChatThreadParticipantDto> Participants { get; set; } = new List<ChatThreadParticipantDto>();
    }
    
    public class ChatThreadParticipantDto : IDto
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public UserType UserType { get; set; }
        public BarberType? BarberType { get; set; } // Store veya FreeBarber için
    }
}
