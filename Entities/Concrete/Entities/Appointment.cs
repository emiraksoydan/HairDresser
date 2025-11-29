
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class Appointment : IEntity
    {
        public Guid Id { get; set; }
        public Guid? ChairId { get; set; }
        public Guid AppointmentFromId { get; set; }
        public Guid? AppointmentToId { get; set; }
        public Guid? WorkerUserId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public AppointmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsLinkedAppointment { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public ICollection<AppointmentServiceOffering> ServiceOfferings { get; set; }

    }
}
