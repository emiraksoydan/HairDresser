using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class CreateAppointmentRequestDto : IDto
    {
        public Guid StoreId { get; set; }
        public Guid? ChairId { get; set; } 
        public DateOnly? AppointmentDate { get; set; } // İsteğime Göre senaryosunda null olabilir
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public Guid? FreeBarberUserId { get; set; } 
        public List<Guid> ServiceOfferingIds { get; set; } = new();

        public double? RequestLatitude { get; set; }
        public double? RequestLongitude { get; set; }

        // Customer -> FreeBarber randevusu için yeni alanlar
        /// <summary>
        /// StoreSelectionType - İsteğime Göre (0) veya Dükkan Seç (1)
        /// Sadece Customer -> FreeBarber randevusunda kullanılır
        /// </summary>
        public StoreSelectionType? StoreSelectionType { get; set; }
        
        /// <summary>
        /// Randevu notu - Müşteri tarafından yazılır (hizmetler, fiyatlar, saat bilgisi)
        /// </summary>
        public string? Note { get; set; }
    }
}
