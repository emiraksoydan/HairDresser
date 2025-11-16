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
        public Guid BarberStoreOwnerId { get; set; }
        public string StoreName { get; set; }
        public string AddressDescription { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public BarberType Type { get; set; }
        public PricingType PricingType { get; set; }
        public double PricingValue { get; set; }
        public string TaxDocumentFilePath { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
