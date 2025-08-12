using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class BarberStore : IEntity
    {
        public Guid Id { get; set; }
        public Guid BarberStoreUserId { get; set; }
        public User BarberStoreUser { get; set; }
        public string StoreName { get; set; }
        public string StoreImageUrl { get; set; }
        public BarberType Type { get; set; }
        public Guid AddressId { get; set; } 
        public AddressInfo Address { get; set; }
        public string PricingType { get; set; }
        public double PricingValue { get; set; }


    }
}
