using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateRequestDtoValidator : AbstractValidator<CreateRequestDto>
    {
        public CreateRequestDtoValidator()
        {
            RuleFor(x => x.RequestTitle)
                .NotEmpty().WithMessage("İstek başlığı boş olamaz.")
                .MaximumLength(200).WithMessage("İstek başlığı 200 karakterden uzun olamaz.");

            RuleFor(x => x.RequestMessage)
                .NotEmpty().WithMessage("İstek mesajı boş olamaz.")
                .MaximumLength(2000).WithMessage("İstek mesajı 2000 karakterden uzun olamaz.");
        }
    }
}
