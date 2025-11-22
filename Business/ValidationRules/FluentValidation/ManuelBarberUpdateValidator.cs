using Entities.Concrete.Dto;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.ValidationRules.FluentValidation
{

    public class ManuelBarberUpdateValidator : AbstractValidator<ManuelBarberUpdateDto>
    {
        public ManuelBarberUpdateValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().WithMessage("Berber adı zorunludur.");
        }
    }
}
