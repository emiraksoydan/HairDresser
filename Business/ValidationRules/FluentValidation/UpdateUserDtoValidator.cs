using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("İsim zorunludur")
                .MinimumLength(2).WithMessage("İsim en az 2 karakter olmalıdır")
                .MaximumLength(20).WithMessage("İsim en fazla 20 karakter olabilir")
                .Matches("^[^\\s]+$").WithMessage("İsim boşluk içeremez");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Soyisim zorunludur")
                .MinimumLength(2).WithMessage("Soyisim en az 2 karakter olmalıdır")
                .MaximumLength(20).WithMessage("Soyisim en fazla 20 karakter olabilir")
                .Matches("^[^\\s]+$").WithMessage("Soyisim boşluk içeremez");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Telefon numarası zorunludur")
                .Length(13).WithMessage("Telefon numarası 13 haneli olmalıdır (+90XXXXXXXXXX formatında)");
        }
    }
}
