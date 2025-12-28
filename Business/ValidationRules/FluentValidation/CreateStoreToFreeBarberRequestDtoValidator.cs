using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateStoreToFreeBarberRequestDtoValidator : AbstractValidator<CreateStoreToFreeBarberRequestDto>
    {
        public CreateStoreToFreeBarberRequestDtoValidator()
        {
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage("Dükkan seçimi zorunludur.");

            RuleFor(x => x.FreeBarberUserId)
                .NotEmpty().WithMessage("Serbest berber seçimi zorunludur.");
        }
    }
}
