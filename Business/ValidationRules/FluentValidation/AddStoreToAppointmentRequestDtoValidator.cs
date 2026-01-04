using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class AddStoreToAppointmentRequestDtoValidator : AbstractValidator<AddStoreToAppointmentRequestDto>
    {
        public AddStoreToAppointmentRequestDtoValidator()
        {
            // StoreId zorunlu
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage("Dükkan seçimi zorunludur.");

            // ChairId zorunlu
            RuleFor(x => x.ChairId)
                .NotEmpty().WithMessage("Koltuk seçimi zorunludur.");

            // AppointmentDate zorunlu
            RuleFor(x => x.AppointmentDate)
                .NotNull().WithMessage("Randevu tarihi zorunludur.");

            // StartTime ve EndTime zorunlu
            RuleFor(x => x.StartTime)
                .NotNull().WithMessage("Başlangıç saati zorunludur.");

            RuleFor(x => x.EndTime)
                .NotNull().WithMessage("Bitiş saati zorunludur.");

            // ServiceOfferingIds zorunlu (en az 1)
            RuleFor(x => x.ServiceOfferingIds)
                .NotEmpty().WithMessage("Hizmet seçimi zorunludur.")
                .Must(ids => ids != null && ids.Count > 0).WithMessage("En az bir hizmet seçilmelidir.");
        }
    }
}

