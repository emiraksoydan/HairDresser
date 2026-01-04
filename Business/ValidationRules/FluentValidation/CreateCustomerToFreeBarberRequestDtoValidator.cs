using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateCustomerToFreeBarberRequestDtoValidator : AbstractValidator<CreateAppointmentRequestDto>
    {
        public CreateCustomerToFreeBarberRequestDtoValidator()
        {
            // FreeBarberUserId zorunlu
            RuleFor(x => x.FreeBarberUserId)
                .NotEmpty().WithMessage("Serbest berber seçimi zorunludur.");

            // StoreSelectionType zorunlu
            RuleFor(x => x.StoreSelectionType)
                .NotNull().WithMessage("Dükkan seç seçilmelidir.")
                .IsInEnum().WithMessage("Geçersiz dükkan seçim tipi.");

            // StoreSelection senaryosu için kurallar
            When(x => x.StoreSelectionType == StoreSelectionType.StoreSelection, () =>
            {
                RuleFor(x => x.Note)
                    .NotEmpty().WithMessage("Randevu notu zorunludur.");

                RuleFor(x => x.StoreId)
                    .Must(storeId => storeId == Guid.Empty || storeId == default)
                    .WithMessage("Dükkan seç senaryosunda storeid gönderilemez.");

                RuleFor(x => x.ServiceOfferingIds)
                    .Must(ids => ids == null || ids.Count == 0)
                    .WithMessage("Dükkan seç senaryosunda hizmet seçilemez.");
            });

            // CustomRequest senaryosu için kurallar
            When(x => x.StoreSelectionType == StoreSelectionType.CustomRequest, () =>
            {
                RuleFor(x => x.ServiceOfferingIds)
                    .NotEmpty().WithMessage("Hizmet seçimi zorunludur.")
                    .Must(ids => ids != null && ids.Count > 0).WithMessage("En az bir hizmet seçilmelidir.");

                RuleFor(x => x.StoreId)
                    .Must(storeId => storeId == Guid.Empty || storeId == default)
                    .WithMessage("İsteğime göre seçeneğinde dükkan seçilemez.");
            });

            // Konum zorunlu
            RuleFor(x => x.RequestLatitude)
                .NotNull().WithMessage("Konum bilgisi (latitude) zorunludur.");

            RuleFor(x => x.RequestLongitude)
                .NotNull().WithMessage("Konum bilgisi (longitude) zorunludur.");
        }
    }
}

