using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Security.PhoneSetting
{
    public class PhoneService : IPhoneService
    {
        public string NormalizeToE164(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var d = new string(raw.Where(char.IsDigit).ToArray());
            if (d.StartsWith("00")) d = d[2..];
            if (d.Length == 10 && d.StartsWith("5")) d = "90" + d; // TR varsayımı
            if (!d.StartsWith("+")) d = "+" + d;
            return d;
        }

        public string Mask(string e164) =>
            string.IsNullOrEmpty(e164) || e164.Length < 6 ? "****"
            : $"{e164[..4]} {new string('*', e164.Length - 6)} {e164[^2..]}";
    }
}
