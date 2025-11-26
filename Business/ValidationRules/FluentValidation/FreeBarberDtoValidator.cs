using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class FreeBarberDtoValidator : AbstractValidator<FreeBarberCreateDto>
    {
        public FreeBarberDtoValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().WithMessage(" Ad zorunludur");
            RuleFor(x => x.LastName).NotEmpty().WithMessage(" Soyad zorunludur");
            RuleFor(x => x.Type).NotNull().WithMessage("İşletme türü zorunludur").IsInEnum().WithMessage("Geçerli bir işletme türü seçilmelidir");
            RuleFor(x => x.Offerings).NotNull().WithMessage("Hizmet listesi zorunludur").Must(x => x.Count > 0).WithMessage("En az bir hizmet girilmelidir");
            RuleForEach(x => x.Offerings).ChildRules(o =>
            {
                o.RuleFor(x => x.ServiceName)
                    .NotEmpty().WithMessage("Hizmet adı boş olamaz");

                o.RuleFor(x => x.Price)
                    .GreaterThan(0).WithMessage("Hizmet fiyatı 0'dan büyük olmalıdır");
            });
            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).WithMessage("Geçerli bir enlem değeri giriniz (-90..90).");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).WithMessage("Geçerli bir boylam değeri giriniz (-180..180).");

            RuleFor(x => x.BarberCertificate)
                .NotEmpty().WithMessage("Vergi levhası zorunludur.");

        }
    }
}
