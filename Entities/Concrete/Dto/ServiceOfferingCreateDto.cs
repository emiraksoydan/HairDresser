using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ServiceOfferingCreateDto : IDto
    {
        public decimal Price { get; set; }
        public string ServiceName { get; set; }
    }
}
