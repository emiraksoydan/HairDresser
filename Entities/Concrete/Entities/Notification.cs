using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class Notification : IEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? AppointmentId { get; set; }

        public NotificationType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Body { get; set; } // opsiyonel

        public string PayloadJson { get; set; } = "{}";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
