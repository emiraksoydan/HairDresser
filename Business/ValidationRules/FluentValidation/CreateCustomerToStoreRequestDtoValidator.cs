using Entities.Concrete.Dto;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateCustomerToStoreRequestDtoValidator : AbstractValidator<CreateAppointmentRequestDto>
    {
        public CreateCustomerToStoreRequestDtoValidator()
        {
            // FreeBarberUserId gönderilmemeli
            RuleFor(x => x.FreeBarberUserId)
                .Must(fbId => !fbId.HasValue)
                .WithMessage("Dükkan randevusunda serbest berber seçilemez.");

            // ChairId zorunlu
            RuleFor(x => x.ChairId)
                .NotEmpty().WithMessage("Koltuk seçimi zorunludur.");

            // StartTime ve EndTime zorunlu
            RuleFor(x => x.StartTime)
                .NotNull().WithMessage("Başlangıç saati zorunludur.");

            RuleFor(x => x.EndTime)
                .NotNull().WithMessage("Bitiş saati zorunludur.");

            // AppointmentDate zorunlu
            RuleFor(x => x.AppointmentDate)
                .NotNull().WithMessage("Randevu tarihi zorunludur.");

            // Konum zorunlu
            RuleFor(x => x.RequestLatitude)
                .NotNull().WithMessage("Konum bilgisi (latitude) zorunludur.");

            RuleFor(x => x.RequestLongitude)
                .NotNull().WithMessage("Konum bilgisi (longitude) zorunludur.");

            // StoreId zorunlu (implicit - ChairId'den kontrol edilebilir ama açıkça belirtelim)
            RuleFor(x => x.StoreId)
                .NotEmpty().WithMessage("Dükkan seçimi zorunludur.");
        }
    }
}

