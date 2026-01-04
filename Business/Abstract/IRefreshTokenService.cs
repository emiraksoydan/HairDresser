using System;

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
