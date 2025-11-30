using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class NotificationRecipient : IEntity
    {
        public Guid Id { get; set; }
        public Guid NotificationId { get; set; }
        public Guid UserId { get; set; }

        public bool IsRead { get; set; }
        public DateTime? ReadAtUtc { get; set; }
    }
}
