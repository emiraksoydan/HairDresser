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
        public Guid SenderId { get; set; }  
        public Guid ReceiverId { get; set; }
        public NotificationType Type { get; set; } 
        public string Payload { get; set; }  
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
