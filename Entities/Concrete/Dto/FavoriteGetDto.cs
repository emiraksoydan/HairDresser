using System;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public enum FavoriteTargetType
    {
        Store = 1,
        FreeBarber = 2,
        Customer = 3,
        ManuelBarber = 4
    }

    public class FavoriteGetDto
    {
        public Guid Id { get; set; }
        public Guid FavoritedFromId { get; set; }
        public Guid FavoritedToId { get; set; }
        public string? TargetName { get; set; }
        public string? TargetImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public FavoriteTargetType TargetType { get; set; }
        
        // Store detayları (TargetType = Store ise dolu)
        public BarberStoreGetDto? Store { get; set; }
        
        // FreeBarber detayları (TargetType = FreeBarber ise dolu)
        public FreeBarberGetDto? FreeBarber { get; set; }
        
        // Customer detayları (TargetType = Customer ise dolu)
        public UserFavoriteDto? Customer { get; set; }
        
        // ManuelBarber detayları (TargetType = ManuelBarber ise dolu)
        public ManuelBarberFavoriteDto? ManuelBarber { get; set; }
    }

    public class UserFavoriteDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? ImageUrl { get; set; }
        public double Rating { get; set; } // Ortalama rating
        public int FavoriteCount { get; set; } // Favori sayısı
        public int ReviewCount { get; set; } // Yorum sayısı
    }

    public class ManuelBarberFavoriteDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string? ImageUrl { get; set; }
    }
}
