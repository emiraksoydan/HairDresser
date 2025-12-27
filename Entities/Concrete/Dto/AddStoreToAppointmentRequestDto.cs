using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class AddStoreToAppointmentRequestDto : IDto
    {
        public Guid StoreId { get; set; }
        public Guid ChairId { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<Guid> ServiceOfferingIds { get; set; } = new();
    }
}

