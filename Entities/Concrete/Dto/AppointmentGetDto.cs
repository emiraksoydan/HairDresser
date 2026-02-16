using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class AppointmentGetDto : IDto
    {
        // --- Temel Randevu Bilgileri ---
        public Guid Id { get; set; }
        public Guid? ChairId { get; set; }

        public string? ChairName { get; set; }
        public TimeSpan? StartTime { get; set; } // İsteğime Göre senaryosunda null olabilir
        public TimeSpan? EndTime { get; set; } // İsteğime Göre senaryosunda null olabilir
        public DateOnly? AppointmentDate { get; set; } // İsteğime Göre senaryosunda null olabilir
        public AppointmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        // --- YENİ: Alınan Hizmetler Listesi ---
        public List<AppointmentServiceDto> Services { get; set; } = new();
        public decimal TotalPrice { get; set; } // Hizmetlerin toplam fiyatı

        // ... (Diğer Store, FreeBarber, ManuelBarber, Customer alanları aynen kalıyor) ...

        public PricingType PricingType { get; set; }
        public double PricingValue { get; set; }
        public AppointmentRequester AppointmentRequester { get; set; }
        public Guid? BarberStoreId { get; set; }
        public Guid? StoreUserId { get; set; } // Dükkan sahibinin User ID'si (şikayet için)
        public Guid? StoreId { get; set; } // Specific store ID for multi-store owners
        public string? StoreName { get; set; }
        public string? StoreImage { get; set; }
        public bool IsStoreFavorite { get; set; }
        public double? MyRatingForStore { get; set; }
        public string? MyCommentForStore { get; set; }
        public string? StoreAddressDescription { get; set; } // Dükkan adres açıklaması

        public BarberType StoreType { get; set; }
        public double? StoreAverageRating { get; set; } // Store'un ortalama rating'i
        public string? StoreOwnerNumber { get; set; } // Dükkan sahibi numarası
        public Guid? FreeBarberId { get; set; }
        public Guid? FreeBarberUserId { get; set; } // FreeBarber'ın User ID'si (şikayet için)
        public string? FreeBarberName { get; set; }
        public string? FreeBarberImage { get; set; }
        public bool IsFreeBarberFavorite { get; set; }
        public double? MyRatingForFreeBarber { get; set; }
        public string? MyCommentForFreeBarber { get; set; }
        public double? FreeBarberAverageRating { get; set; } // FreeBarber'ın ortalama rating'i
        public string? FreeBarberNumber { get; set; } // Serbest berber numarası

        public Guid? ManuelBarberId { get; set; }
        public string? ManuelBarberName { get; set; }
        public string? ManuelBarberImage { get; set; }
        public double? MyRatingForManuelBarber { get; set; }
        public string? MyCommentForManuelBarber { get; set; }
        public double? ManuelBarberAverageRating { get; set; } // ManuelBarber'ın ortalama rating'i

        public Guid? CustomerUserId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerImage { get; set; }
        public string? CustomerNumber { get; set; } // Müşteri numarası
        public bool IsCustomerFavorite { get; set; }
        public double? MyRatingForCustomer { get; set; }
        public string? MyCommentForCustomer { get; set; }
        public double? CustomerAverageRating { get; set; } // Customer'ın ortalama rating'i
        
        // --- Decision Statuses (3'lü sistem için) ---
        public DecisionStatus? StoreDecision { get; set; }
        public DecisionStatus? FreeBarberDecision { get; set; }
        public DecisionStatus? CustomerDecision { get; set; }
        
        // --- StoreSelectionType (3'lü sistem için) ---
        public StoreSelectionType? StoreSelectionType { get; set; }
        
        // --- Note ---
        public string? Note { get; set; }
    }

    // --- YENİ: Hizmet Detayı İçin Küçük DTO ---
    public class AppointmentServiceDto
    {
        public Guid ServiceId { get; set; } // ServiceOfferingId
        public string ServiceName { get; set; }
        public decimal Price { get; set; }
    }
}