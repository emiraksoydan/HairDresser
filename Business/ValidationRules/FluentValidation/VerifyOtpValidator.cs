using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class VerifyOtpValidator : AbstractValidator<UserForVerifyDto>
    {
        public VerifyOtpValidator()
        {
            RuleFor(x => x.PhoneNumber).NotEmpty().NotNull().WithMessage("Telefon numarası zorunludur").Length(13).WithMessage("Telefon numarası 13 haneli olacak");
            RuleFor(x => x.Code).NotEmpty().NotNull().WithMessage("Kod girilmelidir");
            When(x => x.Mode == "register", () =>
            {
                RuleFor(x => x.FirstName)
                    .NotEmpty().WithMessage("İsim gerekli");

                RuleFor(x => x.LastName)
                    .NotEmpty().WithMessage("Soyisim gerekli");

                RuleFor(x => x.CertificateFilePath)
                    .NotEmpty().WithMessage("Sertifika dosyası zorunludur.")
                    .Unless(x => x.UserType == UserType.Customer);
            });
        }
    }
}
