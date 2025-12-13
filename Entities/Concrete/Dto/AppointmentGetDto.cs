using Entities.Concrete.Enums;
using Entities.Abstract;
using System;

namespace Entities.Concrete.Dto
{
    public class AppointmentGetDto : IDto
    {
        // --- Temel Randevu Bilgileri ---
        public Guid Id { get; set; }
        public Guid? ChairId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public AppointmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        // --- YENİ: Alınan Hizmetler Listesi ---
        public List<AppointmentServiceDto> Services { get; set; } = new();
        public decimal TotalPrice { get; set; } // Hizmetlerin toplam fiyatı

        // ... (Diğer Store, FreeBarber, ManuelBarber, Customer alanları aynen kalıyor) ...
        public Guid? BarberStoreId { get; set; }
        public string? StoreName { get; set; }
        public string? StoreImage { get; set; }
        public bool IsStoreFavorite { get; set; }
        public double? MyRatingForStore { get; set; }
        public string? MyCommentForStore { get; set; }

        public Guid? FreeBarberId { get; set; }
        public string? FreeBarberName { get; set; }
        public string? FreeBarberImage { get; set; }
        public bool IsFreeBarberFavorite { get; set; }
        public double? MyRatingForFreeBarber { get; set; }
        public string? MyCommentForFreeBarber { get; set; }

        public Guid? ManuelBarberId { get; set; }
        public string? ManuelBarberName { get; set; }
        public string? ManuelBarberImage { get; set; }
        public double? MyRatingForManuelBarber { get; set; }
        public string? MyCommentForManuelBarber { get; set; }

        public Guid? CustomerUserId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerImage { get; set; }
        public bool IsCustomerFavorite { get; set; }
        public double? MyRatingForCustomer { get; set; }
        public string? MyCommentForCustomer { get; set; }
    }

    // --- YENİ: Hizmet Detayı İçin Küçük DTO ---
    public class AppointmentServiceDto
    {
        public Guid ServiceId { get; set; } // ServiceOfferingId
        public string ServiceName { get; set; }
        public decimal Price { get; set; }
    }
}