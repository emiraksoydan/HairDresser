using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class ServiceOffering : IEntity
    {

        public Guid Id { get; set; }
        public Guid OwnerId { get; set; } 
       
        public decimal Price { get; set; }
        public string ServiceName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
