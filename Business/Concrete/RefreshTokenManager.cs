using Business.Abstract;
using Core.Utilities.Security.Hashing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private const int FingerprintBytes = 12; // 12 byte (~16-24 b64url char)

        public (string Plain, byte[] Hash, byte[] Salt, DateTime Expires, string Fingerprint)
            CreateNew(int days)
        {
            var plain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); // 256-bit
            HashingHelper.CreateHash(plain, out var hash, out var salt);
            var fp = MakeFingerprint(plain);
            var expires = DateTime.UtcNow.AddDays(days);
            return (plain, hash, salt, expires, fp);
        }

        public bool Verify(string plain, byte[] hash, byte[] salt)
            => HashingHelper.verifyValueHash(plain, hash, salt);

        public string MakeFingerprint(string plain)
        {
            using var sha = SHA256.Create();
            var full = sha.ComputeHash(Encoding.UTF8.GetBytes(plain));
            var slice = full.AsSpan(0, FingerprintBytes).ToArray();
            return Convert.ToBase64String(slice).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
