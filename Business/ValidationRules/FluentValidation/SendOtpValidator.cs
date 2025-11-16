using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.ValidationRules.FluentValidation
{
    public class SendOtpValidator : AbstractValidator<string>
    {
        public SendOtpValidator()
        {
            RuleFor(x=>x).NotEmpty().NotNull().WithMessage("Telefon numarası zorunludur")
           .Length(13).WithMessage("Telefon numarası 13 haneli olacak");
        }
    }
}
