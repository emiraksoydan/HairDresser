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
    public class BarberStoreDetail : IDto
    {
        public Guid Id { get; set; }
        public string StoreName { get; set; }
        public List<ImageGetDto> ImageList { get; set; }
        public string Type { get; set; }
        public string PricingType { get; set; }
        public double PricingValue { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsOpenNow { get; set; }

        public string TaxDocumentFilePath { get; set; }
        public string AddressDescription { get; set; }
        public List<BarberChairDto> BarberStoreChairs { get; set; }
        public List<ManuelBarberDto> ManuelBarbers { get; set; }
        public List<ServiceOfferingGetDto> ServiceOfferings { get; set; }
        public List<WorkingHourDto> WorkingHours { get; set; }
    }
}
