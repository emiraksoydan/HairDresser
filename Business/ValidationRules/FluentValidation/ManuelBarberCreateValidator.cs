using Entities.Concrete.Dto;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.ValidationRules.FluentValidation
{
    public class ManuelBarberCreateValidator : AbstractValidator<ManuelBarberCreateDto>
    {
        public ManuelBarberCreateValidator()
        {
            RuleFor(x=>x.FullName).NotEmpty().WithMessage("Berber adı zorunludur.");
        }
    }
}
