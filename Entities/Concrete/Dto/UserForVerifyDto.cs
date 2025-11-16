using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class UserForVerifyDto : IDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string CertificateFilePath { get; set; }    
        public string Code { get; set; }
        public string? Device {  get; set; }
        public UserType UserType { get; set; }
        public string Mode { get; set; }

        public string? Password { get; set; }
    }
}
