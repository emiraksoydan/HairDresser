using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class FreeBarber : IEntity
    {
        public Guid Id { get; set; }
        public Guid FreeBarberUserId { get; set; }
        public User FreeBarberUser { get; set; }
        public Guid AddressId { get; set; }
        public string FreeBarberImageUrl { get; set; }
        public string FullName { get; set; }    
        public AddressInfo Address { get; set; }
        
        public bool IsAvailable { get; set; }
        public BarberType Type { get; set; }

    }
}
