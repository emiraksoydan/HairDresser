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
    public class UserRegisterValidator : AbstractValidator<UserForRegisterDto>
    {
        public UserRegisterValidator()
        {

            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().WithMessage("Şifre boş olamaz");
            RuleFor(x => x.FirstName).NotEmpty();
            RuleFor(x => x.LastName).NotEmpty();
            RuleFor(x => x.IdentityNumber)
           .NotEmpty().WithMessage("TC Kimlik numarası zorunludur.")
           .Length(11).WithMessage("TC Kimlik numarası 11 haneli olmalıdır.")
           .Matches("^[0-9]+$").WithMessage("TC Kimlik numarası sadece rakamlardan oluşmalıdır.")
           .Must(x => x[0] != '0').WithMessage("TC Kimlik numarası 0 ile başlayamaz.");

            When(x => x.UserType == UserType.FreeBarber || x.UserType == UserType.BarberStore, () =>
            {
                RuleFor(x => x.CertificateFilePath).NotEmpty().WithMessage("Sertifika dosyası zorunludur.");
            });

            //When(x => x.UserType == UserType.BarberStore, () =>
            //{
            //    RuleFor(x => x.TaxDocumentFilePath).NotEmpty().WithMessage("Vergi levhası zorunludur.");
            //});
        }
    }
}
