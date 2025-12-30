using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class UpdateImageDtoValidator : AbstractValidator<UpdateImageDto>
    {
        public UpdateImageDtoValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Resim ID'si boş olamaz");

            RuleFor(x => x.ImageUrl)
                .NotEmpty().WithMessage("Resim URL'i boş olamaz")
                .MaximumLength(2000).WithMessage("Resim URL'i en fazla 2000 karakter olabilir");

            RuleFor(x => x.ImageOwnerId)
                .NotEmpty().WithMessage("Resim sahibi ID'si boş olamaz");

            When(x => x.OwnerType.HasValue, () =>
            {
                RuleFor(x => x.OwnerType)
                    .IsInEnum().WithMessage("Geçerli bir sahip türü seçilmelidir");
            });
        }
    }
}
