using System;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class HelpGuide : IEntity
    {
        public Guid Id { get; set; }
        public int UserType { get; set; } // UserType enum değeri (0: Customer, 1: FreeBarber, 2: BarberStore)
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; } = 0; // Sıralama için
        public bool IsActive { get; set; } = true; // Aktif/pasif durumu
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
