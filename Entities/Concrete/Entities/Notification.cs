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
        public NotificationType Type { get; set; }

        public string Title { get; set; } = default!;
        public string Body { get; set; } = default!;
        public string DataJson { get; set; } = "{}";
        public DateTime CreatedAtUtc { get; set; }

        // idempotency anahtarı (aynı olayı tekrar basmayı engeller)
        public string CorrelationKey { get; set; } = default!;
    }
}

