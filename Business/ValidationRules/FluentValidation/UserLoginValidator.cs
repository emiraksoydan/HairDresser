using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class UserLoginValidator : AbstractValidator<UserForSendOtpDto>
    {
        public UserLoginValidator()
        {
            RuleFor(x => x.PhoneNumber).NotEmpty().NotNull().WithMessage("Numara boş olamaz");
        }
    }
}
