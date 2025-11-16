using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Security.PhoneSetting
{
    public class PhoneService : IPhoneService
    {
        private readonly byte[] _pepperKey;
        private readonly byte[] _aesKey;

        public PhoneService(IOptions<SecurityOption> opt)
        {
            _pepperKey = Convert.FromBase64String(opt.Value.PhonePepperBase64);
            _aesKey = Convert.FromBase64String(opt.Value.PhoneEncKeyBase64);
        }

        public string NormalizeToE164(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var d = new string(raw.Where(char.IsDigit).ToArray());
            if (d.StartsWith("00")) d = d[2..];
            if (d.Length == 10 && d.StartsWith("5")) d = "90" + d; // TR varsayımı
            if (!d.StartsWith("+")) d = "+" + d;
            return d;
        }

        public byte[] ComputeSearchToken(string e164)
        {
            using var h = new HMACSHA256(_pepperKey);
            return h.ComputeHash(Encoding.UTF8.GetBytes(e164));
        }

        public (byte[] cipherPlusTag, byte[] nonce) Encrypt(string e164)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var pt = Encoding.UTF8.GetBytes(e164);
            var ct = new byte[pt.Length];
            var tag = new byte[16];
            using var gcm = new AesGcm(_aesKey);
            gcm.Encrypt(nonce, pt, ct, tag);

            var outBuf = new byte[ct.Length + tag.Length];
            Buffer.BlockCopy(ct, 0, outBuf, 0, ct.Length);
            Buffer.BlockCopy(tag, 0, outBuf, ct.Length, tag.Length);
            return (outBuf, nonce);
        }

        public string Decrypt(byte[] cipherPlusTag, byte[] nonce)
        {
            var tagLen = 16;
            var ctLen = cipherPlusTag.Length - tagLen;
            var ct = new byte[ctLen];
            var tag = new byte[tagLen];
            Buffer.BlockCopy(cipherPlusTag, 0, ct, 0, ctLen);
            Buffer.BlockCopy(cipherPlusTag, ctLen, tag, 0, tagLen);

            var pt = new byte[ctLen];
            using var gcm = new AesGcm(_aesKey);
            gcm.Decrypt(nonce, ct, tag, pt);
            return Encoding.UTF8.GetString(pt);
        }

        public string Mask(string e164) =>
            string.IsNullOrEmpty(e164) || e164.Length < 6 ? "****"
            : $"{e164[..4]} {new string('*', e164.Length - 6)} {e164[^2..]}";
    }
}
