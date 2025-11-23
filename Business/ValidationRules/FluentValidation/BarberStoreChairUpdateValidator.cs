using Entities.Concrete.Dto;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.ValidationRules.FluentValidation
{
    public class BarberStoreChairUpdateValidator : AbstractValidator<BarberChairUpdateDto>
    {
        public BarberStoreChairUpdateValidator()
        {
            RuleFor(x => x.BarberId)
           .NotEmpty()
           .When(x => string.IsNullOrWhiteSpace(x.Name))
           .WithMessage("İsim boş ise mutlaka bir berber seçmelisiniz.");

            RuleFor(x => x.Name)
                .Must(name => string.IsNullOrWhiteSpace(name))
                .When(x => x.BarberId != null && x.BarberId != Guid.Empty)
                .WithMessage("Berber seçili ise koltuk ismi boş olmalıdır.");

            RuleFor(x => x)
                .Must(x =>
                {
                    var hasName = !string.IsNullOrWhiteSpace(x.Name);
                    var hasBarber = x.BarberId != null && x.BarberId != Guid.Empty;
                    return hasName ^ hasBarber;
                })
                .WithMessage("Koltuk için ya isim ya berber seçmelisiniz; ikisi birden veya ikisi de boş olamaz.");
        }
    }
}
