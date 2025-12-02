using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class ChatMessageDto : IDto
    {
        public Guid AppointmentId { get; set; }
        public Guid MessageId { get; set; }
        public Guid SenderUserId { get; set; }
        public string Text { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
