using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class Complaint : IEntity
    {
        public Guid Id { get; set; }
        public Guid ComplaintFromUserId { get; set; }
        public Guid ComplaintToUserId { get; set; }
        public string ComplaintReason { get; set; }
    }
}
