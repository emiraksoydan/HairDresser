using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class ChatThread : IEntity
    {
        public Guid Id { get; set; }
        public Guid AppointmentId { get; set; }

        public Guid? CustomerUserId { get; set; }
        public Guid? StoreOwnerUserId { get; set; }
        public Guid? FreeBarberUserId { get; set; }

        public int CustomerUnreadCount { get; set; }
        public int StoreUnreadCount { get; set; }
        public int FreeBarberUnreadCount { get; set; }

        public DateTime? LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
