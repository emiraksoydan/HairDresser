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
    public class BarberStoreDetailDto : IDto
    {
        public Guid Id { get; set; }
        public string StoreName { get; set; }
        public string StoreImageUrl { get; set; }
        public BarberType Type { get; set; }
        public string PricingType { get; set; }
        public double PricingValue { get; set; }

        public double Rating { get; set; }
        public int FavoriteCount { get; set; }
        public int ReviewCount { get; set; }
        public List<ServiceOfferingGetDto> ServiceOfferings { get; set; }
        public List<WorkingHourDto> WorkingHours { get; set; }
        public bool IsOpenNow { get; set; }
    }

}
