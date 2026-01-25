using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateComplaintDtoValidator : AbstractValidator<CreateComplaintDto>
    {
        public CreateComplaintDtoValidator()
        {
            RuleFor(x => x.ComplaintToUserId)
                .NotEmpty().WithMessage("Şikayet edilecek kullanıcı seçilmelidir.");

            RuleFor(x => x.ComplaintReason)
                .MaximumLength(1000).WithMessage("Şikayet nedeni 1000 karakterden uzun olamaz.");
        }
    }
}
