using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class UserForRegisterDto : IDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string? IdentityNumber { get; set; }
        public string? CertificateFilePath { get; set; }     
        public string? TaxDocumentFilePath { get; set; }
        public UserType UserType { get; set; }
    }
}
