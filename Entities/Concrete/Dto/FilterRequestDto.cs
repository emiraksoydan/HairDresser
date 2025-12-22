using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Filtreleme ve arama için request DTO
    /// </summary>
    public class FilterRequestDto : IDto
    {
        // Konum bilgileri (nearby için)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double DistanceKm { get; set; } = 1.0; // km

        // Arama
        public string? SearchQuery { get; set; }

        // Kullanıcı türü filtresi
        public string? UserType { get; set; } // "Hepsi", "Serbest Berber", "Dükkan"

        // Ana kategori filtresi (BarberType)
        public BarberType? MainCategory { get; set; } // null = Hepsi

        // Hizmet filtresi (CategoryId listesi)
        public List<Guid>? ServiceIds { get; set; }

        // Fiyat filtresi
        public string? PriceSort { get; set; } // "none", "asc", "desc"
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // Pricing Type (Store için)
        public string? PricingType { get; set; } // "all", "rent", "percent"

        // Müsaitlik (FreeBarber için)
        public bool? IsAvailable { get; set; }
        
        // Açık/Kapalı durumu (Store için)
        public bool? IsOpenNow { get; set; }

        // Puanlama
        public int? MinRating { get; set; } // 0-5

        // Favoriler
        public bool? FavoritesOnly { get; set; }
        
        // Kullanıcı ID (favoriler ve diğer kullanıcıya özel filtreler için)
        public Guid? CurrentUserId { get; set; }
    }
}

