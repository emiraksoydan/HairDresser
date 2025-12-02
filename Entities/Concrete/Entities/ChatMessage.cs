using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class ChatMessage : IEntity
    {
        public Guid Id { get; set; }
        public Guid ThreadId { get; set; }
        public Guid AppointmentId { get; set; }

        public Guid SenderUserId { get; set; }
        public string Text { get; set; } = default!;
        public bool IsSystem { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
