using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class RefreshToken : IEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public byte[] TokenHash { get; set; } = default!;
        public byte[] TokenSalt { get; set; } = default!;
        public string Fingerprint { get; set; } = default!;
        public Guid FamilyId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByIp { get; set; }
        public string? Device { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedByIp { get; set; }
        public string? ReplacedByFingerprint { get; set; }

    }
}
