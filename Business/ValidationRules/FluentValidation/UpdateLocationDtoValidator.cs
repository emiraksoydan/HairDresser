using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class UpdateLocationDtoValidator : AbstractValidator<UpdateLocationDto>
    {
        public UpdateLocationDtoValidator()
        {
            // Id artık zorunlu değil - CurrentUserId kullanılıyor
            // RuleFor(x => x.Id) kaldırıldı

            // Latitude zorunlu ve geçerli aralıkta
            RuleFor(x => x.Latitude)
                .NotNull().WithMessage("Enlem (latitude) zorunludur.")
                .InclusiveBetween(-90, 90).WithMessage("Enlem değeri -90 ile 90 arasında olmalıdır.");

            // Longitude zorunlu ve geçerli aralıkta
            RuleFor(x => x.Longitude)
                .NotNull().WithMessage("Boylam (longitude) zorunludur.")
                .InclusiveBetween(-180, 180).WithMessage("Boylam değeri -180 ile 180 arasında olmalıdır.");
        }
    }
}

