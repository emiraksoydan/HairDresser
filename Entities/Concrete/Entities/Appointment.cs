
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class Appointment : IEntity
    {
        public Guid Id { get; set; }
        public Guid? ChairId { get; set; }
        public string? ChairName { get; set; }
        public TimeSpan? StartTime { get; set; } // İsteğime Göre senaryosunda null olabilir
        public TimeSpan? EndTime { get; set; } // İsteğime Göre senaryosunda null olabilir
        public DateOnly? AppointmentDate { get; set; } // İsteğime Göre senaryosunda null olabilir
        public AppointmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? BarberStoreUserId { get; set; }
        /// <summary>
        /// Specific store ID - required for multi-store owners to identify which store is involved
        /// </summary>
        public Guid? StoreId { get; set; }
        public Guid? CustomerUserId { get; set; }
        public Guid? FreeBarberUserId { get; set; }
        public Guid? ManuelBarberId { get; set; }
        public AppointmentRequester RequestedBy { get; set; }
        public StoreSelectionType? StoreSelectionType { get; set; }
        public DecisionStatus? StoreDecision { get; set; }
        public DecisionStatus? FreeBarberDecision { get; set; }
        public DecisionStatus? CustomerDecision { get; set; }
        public DateTime? PendingExpiresAt { get; set; }
        public Guid? CancelledByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public byte[]? RowVersion { get; set; }
        /// <summary>
        /// Randevu notu - Müşteri tarafından yazılır (Customer -> FreeBarber randevusunda)
        /// </summary>
        public string? Note { get; set; }
        
        // Soft Delete: Her kullanıcı tipi için ayrı soft delete alanı
        public bool IsDeletedByCustomerUserId { get; set; } = false;
        public bool IsDeletedByBarberStoreUserId { get; set; } = false;
        public bool IsDeletedByFreeBarberUserId { get; set; } = false;
        
        public ICollection<AppointmentServiceOffering> ServiceOfferings { get; set; } = new List<AppointmentServiceOffering>();
    }
}
