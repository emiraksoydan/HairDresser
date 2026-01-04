using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateFreeBarberToStoreRequestDtoValidator : AbstractValidator<CreateAppointmentRequestDto>
    {
        public CreateFreeBarberToStoreRequestDtoValidator()
        {
            // StartTime ve EndTime zorunlu
            RuleFor(x => x.StartTime)
                .NotNull().WithMessage("Başlangıç saati zorunludur.");

            RuleFor(x => x.EndTime)
                .NotNull().WithMessage("Bitiş saati zorunludur.");

            // AppointmentDate zorunlu
            RuleFor(x => x.AppointmentDate)
                .NotNull().WithMessage("Randevu tarihi zorunludur.");

            // StoreId zorunlu
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage("Dükkan seçimi zorunludur.");

            // FreeBarberUserId gönderilmemeli (metod parametresinden geliyor)
            RuleFor(x => x.FreeBarberUserId)
                .Must(fbId => !fbId.HasValue)
                .WithMessage("Serbest berber ID'si request body'de gönderilmemelidir.");

            // StoreSelectionType gönderilmemeli
            RuleFor(x => x.StoreSelectionType)
                .Must(st => !st.HasValue)
                .WithMessage("Dükkan seçim tipi bu senaryoda kullanılamaz.");
        }
    }
}

