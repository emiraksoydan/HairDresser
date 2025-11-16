using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Security.PhoneSetting
{
    public interface IPhoneService
    {
        string NormalizeToE164(string raw);
        byte[] ComputeSearchToken(string e164);
        (byte[] cipherPlusTag, byte[] nonce) Encrypt(string e164);
        string Decrypt(byte[] cipherPlusTag, byte[] nonce);
        string Mask(string e164);
    }
}
