using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class User : IEntity
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public byte[] PasswordSalt { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] IdentityNumberHash { get; set; }
        public byte[] IdentityNumberSalt { get; set; }
        public string? ProfileImage { get; set; }
        public string? CertificateFilePath { get; set; }
        public string? TaxDocumentFilePath { get; set; }
        public bool Status { get; set; }
        public UserType UserType { get; set; }

        public ICollection<UserOperationClaim> UserOperationClaims { get; set; }
    }
}
