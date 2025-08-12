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
    public class FreeBarberUpdateDto : IDto
    {
        public Guid Id { get; set; }
        public string FreeBarberImageUrl { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public BarberType Type { get; set; }
        public AddressInfo Address { get; set; }
        public List<ServiceOfferingUpdateDto> Offerings { get; set; }
        public List<WorkingHourUpdateDto> WorkingHours { get; set; }
    }
}
