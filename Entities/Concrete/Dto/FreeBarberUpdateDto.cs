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
        public Guid FreeBarberUserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public BarberType Type { get; set; }
        public List<UpdateImageDto> FreeBarberImageList { get; set; }
        public List<ServiceOfferingUpdateDto> Offerings { get; set; }
        public string BarberCertificate { get; set; }


    }
}
