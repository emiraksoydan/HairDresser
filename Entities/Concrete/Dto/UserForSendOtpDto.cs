using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class UserForSendOtpDto : IDto
    {
        public string PhoneNumber { get; set; }
        public UserType UserType { get; set; }
        public OtpPurpose OtpPurpose { get; set; }
    }
}
