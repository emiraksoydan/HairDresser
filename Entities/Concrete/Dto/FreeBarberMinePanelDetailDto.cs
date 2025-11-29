using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class FreeBarberMinePanelDetailDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public BarberType Type { get; set; }
        public bool IsAvailable { get; set; }
        public string BarberCertificate { get; set; }
        public List<ImageGetDto> ImageList { get; set; }
        public List<ServiceOfferingGetDto> Offerings { get; set; }

    }
}
