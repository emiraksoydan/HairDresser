using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class ChatMessageItemDto : IDto
    {
        public Guid MessageId { get; set; }
        public Guid SenderUserId { get; set; }
        public string Text { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
