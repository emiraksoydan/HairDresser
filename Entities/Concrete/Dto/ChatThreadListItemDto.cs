using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class ChatThreadListItemDto : IDto
    {
        public Guid AppointmentId { get; set; }
        public AppointmentStatus Status { get; set; }

        public string Title { get; set; } = default!; // UI’da göstereceğin başlık
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAt { get; set; }

        public int UnreadCount { get; set; }
    }
}
