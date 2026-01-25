using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class Blocked : IEntity
    {
        public Guid Id { get; set; }
        public Guid BlockedFromUserId { get; set; }  // Engelleyen kullanıcı
        public Guid BlockedToUserId { get; set; }    // Engellenen kullanıcı
        public string BlockReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
