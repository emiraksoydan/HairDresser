using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class Notification : IEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }  
        public Guid CorrelationId { get; set; }
        public NotificationType Type { get; set; } 
        public string Topic { get; set; } = "Appointment";
        public string Payload { get; set; } = "";  
        public bool IsRead { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
