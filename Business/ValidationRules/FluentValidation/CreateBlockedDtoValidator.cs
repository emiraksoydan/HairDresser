using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateBlockedDtoValidator : AbstractValidator<CreateBlockedDto>
    {
        public CreateBlockedDtoValidator()
        {
            RuleFor(x => x.BlockedToUserId)
                .NotEmpty().WithMessage("Engellenecek kullanıcı seçilmelidir.");

            RuleFor(x => x.BlockReason)
                .MaximumLength(500).WithMessage("Engelleme nedeni 500 karakterden uzun olamaz.");
        }
    }
}
