using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class BarberStoreOperationDetail : IDto
    {
        public Guid Id { get; set; }
        public string StoreName { get; set; }
        public string StoreImageUrl { get; set; }
        public BarberType Type { get; set; }
        public string PricingType { get; set; }
        public double PricingValue { get; set; }
        public AddressInfo Address { get; set; }
        public List<ServiceOfferingListDto> ServiceOfferings { get; set; }
        public List<WorkingHourDto> WorkingHours { get; set; }
        public List<ManuelBarberOperation> BarberOperations { get; set; }
        public List<BarberChairDto> BarberChairs { get; set; }
    }
}
