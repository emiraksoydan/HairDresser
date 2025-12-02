
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class Appointment : IEntity
    {
        public Guid Id { get; set; }
        public Guid? ChairId { get; set; } 
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public AppointmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? BarberStoreUserId { get; set; }
        public Guid? CustomerUserId { get; set; }
        public Guid? FreeBarberUserId { get; set; }
        public Guid? ManuelBarberId { get; set; }
        public AppointmentRequester RequestedBy { get; set; }
        public DecisionStatus StoreDecision { get; set; } = DecisionStatus.Pending;
        public DecisionStatus FreeBarberDecision { get; set; } = DecisionStatus.Pending;
        public DateTime? PendingExpiresAt { get; set; }
        public Guid? CancelledByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public byte[]? RowVersion { get; set; }
        public ICollection<AppointmentServiceOffering> ServiceOfferings { get; set; } = new List<AppointmentServiceOffering>();
    }
}
