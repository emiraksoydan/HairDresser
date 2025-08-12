using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class Appointment : IEntity
    {
        public Guid Id { get; set; }
        public Guid? StoreId { get; set; }
        public Guid? ChairId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid PerformerUserId { get; set; }
        public Guid BookedByUserId { get; set; }
        public UserType BookedByType { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndTime { get; set; }
        public AppointmentStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
