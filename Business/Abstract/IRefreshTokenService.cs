using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IRefreshTokenService
    {
        (string Plain, byte[] Hash, byte[] Salt, DateTime Expires, string Fingerprint)
            CreateNew(int days);
        bool Verify(string plain, byte[] hash, byte[] salt);
        string MakeFingerprint(string plain);
    }
}
