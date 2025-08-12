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
    public class BarberStoreCreateDto : IDto
    {
        public string StoreName { get; set; }
        public string StoreImageUrl { get; set; }
        public BarberType? Type { get; set; }
        public string PricingType { get; set; }
        public double? PricingValue { get; set; }
        public AddressInfo Address { get; set; }
        public List<BarberChairCreateDto> Chairs { get; set; }
        public List<ServiceOfferingCreateDto> Offerings { get; set; }
        public List<ManuelBarberCreateDto> ManualBarbers { get; set; }
        public List<WorkingHourCreateDto> WorkingHours { get; set; }


    }

}
