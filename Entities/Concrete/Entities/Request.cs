using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class Request : IEntity
    {
        public Guid Id { get; set; }
        public Guid RequestFromUserId { get; set; }
        public Guid RequestToUserId { get; set; }
        public string RequestReason { get; set; }
    }
}
