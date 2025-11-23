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
    public class BarberStoreUpdateDto : IDto
    {
        public Guid Id { get; set; }
        public string StoreName { get; set; }
        public BarberType Type { get; set; }
        public PricingType PricingType { get; set; }
        public string AddressDescription { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double PricingValue { get; set; }
        public string TaxDocumentFilePath { get; set; }
        public List<UpdateImageDto>? StoreImageList { get; set; }
        public List<BarberChairUpdateDto> Chairs { get; set; }
        public List<ManuelBarberUpdateDto> ManuelBarbers { get; set; }
        public List<ServiceOfferingUpdateDto> Offerings { get; set; }
        public List<WorkingHourUpdateDto> WorkingHours { get; set; }


    }

}
