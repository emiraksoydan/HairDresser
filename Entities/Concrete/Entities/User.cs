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
        public byte[] PhoneEncrypted { get; set; } 
        public byte[] PhoneEncryptedNonce { get; set; } 
        public byte[] PhoneSearchToken { get; set; } 
        public string CertificateFilePath { get; set; }
        public bool IsActive { get; set; }
        public Guid? ImageId { get; set; }
        public Image Image { get; set; }
        public UserType UserType { get; set; }
        public ICollection<UserOperationClaim> UserOperationClaims { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
