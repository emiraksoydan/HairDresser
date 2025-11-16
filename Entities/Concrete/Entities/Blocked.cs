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
        public Guid BlockedToUserId { get; set; }
        public Guid BlockedFromUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BlcokReason { get; set; }
    }
}
