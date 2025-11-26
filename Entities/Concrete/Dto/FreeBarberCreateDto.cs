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
    public class FreeBarberCreateDto : IDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public BarberType Type { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<CreateImageDto> FreeBarberImageList { get; set; }
        public List<ServiceOfferingCreateDto> Offerings { get; set; }
        public string BarberCertificate { get; set; }



    }
}
