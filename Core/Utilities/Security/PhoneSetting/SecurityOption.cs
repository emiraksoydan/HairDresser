using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Security.PhoneSetting
{
    public class SecurityOption
    {
        public string PhonePepperBase64 { get; set; } = default!; 
        public string PhoneEncKeyBase64 { get; set; } = default!;
    }
}
