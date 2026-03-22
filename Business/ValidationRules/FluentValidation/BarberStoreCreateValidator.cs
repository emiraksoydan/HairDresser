using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using FluentValidation;
using System.Globalization;
using System.Linq;

public class BarberStoreCreateDtoValidator : AbstractValidator<BarberStoreCreateDto>
{
    public BarberStoreCreateDtoValidator()
    {
        // Temel alanlar
        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("İşletme adı zorunludur.")
            .MinimumLength(2).WithMessage("İşletme adı en az 2 karakter olmalıdır.")
            .MaximumLength(100).WithMessage("İşletme adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Geçerli bir işletme türü seçilmelidir.");

        RuleFor(x => x.PricingType)
            .IsInEnum().WithMessage("Geçerli bir koltuk fiyat hizmeti seçilmelidir.");

        RuleFor(x => x.AddressDescription)
            .NotEmpty().WithMessage("Adres açıklaması zorunludur.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Geçerli bir enlem değeri giriniz (-90..90).");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Geçerli bir boylam değeri giriniz (-180..180).");

        RuleFor(x => x.TaxDocumentImageId)
            .NotNull().WithMessage("Vergi levhası resmi zorunludur.")
            .NotEmpty().WithMessage("Vergi levhası resmi zorunludur.");

        // PricingValue koşullu
        When(x => x.PricingType == PricingType.Rent, () =>
        {
            RuleFor(x => x.PricingValue)
                .NotNull().WithMessage("Fiyat girilmelidir.")
                .GreaterThanOrEqualTo(0).WithMessage("Fiyat 0'dan veya eşit   olmalıdır.");
        });

        When(x => x.PricingType == PricingType.Percent, () =>
        {
            RuleFor(x => x.PricingValue)
                .NotNull().WithMessage("Yüzdelik girilmelidir.")
                .GreaterThan(0).WithMessage("Yüzdelik 0'dan büyük olmalıdır.")
                .LessThanOrEqualTo(100).WithMessage("Yüzdelik 100'ü geçemez.");
        });

        // Chairs
        RuleFor(x => x.Chairs)
             .NotNull()
             .NotEmpty()
             .WithMessage("En az bir koltuk eklenmelidir.");

        RuleForEach(x => x.Chairs).ChildRules(ch =>
        {
            ch.RuleFor(c => c.Name)
              .NotEmpty()
              .When(c => c.BarberId == null)
              .WithMessage("Berber atanmadıysa koltuk adı zorunludur.");
        });

        // ManuelBarbers
        RuleForEach(x => x.ManuelBarbers).ChildRules(b =>
       {
           b.RuleFor(m => m.FullName)
            .NotEmpty()
            .WithMessage("Manuel berber adı zorunludur.");
       });

        // Berberlerin toplamı 30'u geçmemeli
        RuleFor(x => x.ManuelBarbers)
            .Must(barbers => (barbers?.Count ?? 0) <= 30)
            .WithMessage("Berber sayısı 30'u geçemez.");

        // Koltukların toplamı 30'u geçmemeli
        RuleFor(x => x.Chairs)
            .Must(chairs => (chairs?.Count ?? 0) <= 30)
            .WithMessage("Koltuk sayısı 30'u geçemez.")
            .When(x => x.Chairs != null);

        // Offerings
        RuleFor(x => x.Offerings)
            .NotEmpty().WithMessage("En az bir hizmet girilmelidir.");

        RuleForEach(x => x.Offerings).ChildRules(o =>
        {
            o.RuleFor(v => v.ServiceName)
             .NotEmpty().WithMessage("Hizmet adı boş olamaz.");

            o.RuleFor(v => v.Price)
             .NotNull().WithMessage("Hizmet fiyatı girilmelidir")
             .GreaterThanOrEqualTo(0).WithMessage("Hizmet fiyatı 0 veya daha büyük olmalıdır");
        });

        // Hizmet adları benzersiz (case-insensitive)
        RuleFor(x => x.Offerings)
            .Must(list => list.Select(i => i.ServiceName?.Trim().ToLowerInvariant())
                              .Where(s => !string.IsNullOrWhiteSpace(s))
                              .GroupBy(s => s!)
                              .All(g => g.Count() == 1))
            .WithMessage("Hizmet adları benzersiz olmalıdır.");

        // Working hours
        RuleFor(x => x.WorkingHours)
            .NotNull().WithMessage("Çalışma saatleri zorunludur.")
            .Must(w => w.Count > 0).WithMessage("En az bir çalışma günü girilmelidir.");

        // Aynı güne iki kayıt olmasın
        RuleFor(x => x.WorkingHours!)
            .Must(list =>
            {
                var groups = list.GroupBy(i => i.DayOfWeek);
                return groups.All(g => g.Count() == 1);
            })
            .WithMessage("Her gün için tek bir çalışma kaydı olmalıdır.");

        // Saat detay kuralları (kapalı olmayan günlerde)
        RuleForEach(x => x.WorkingHours!)
            .Where(w => !w.IsClosed)
            .ChildRules(c =>
            {
                c.RuleFor(w => w.StartTime)
                    .NotEmpty().WithMessage("Başlangıç saati zorunludur.")
                    .Must(IsHHmm).WithMessage("Başlangıç saati HH:mm formatında olmalı.");

                c.RuleFor(w => w.EndTime)
                    .NotEmpty().WithMessage("Bitiş saati zorunludur.")
                    .Must(IsHHmm).WithMessage("Bitiş saati HH:mm formatında olmalı.");

                c.RuleFor(w => w)
                    .Must(w => TryParseHHmm(w.StartTime, out var s) &&
                               TryParseHHmm(w.EndTime, out var e) &&
                               s < e)
                    .WithMessage("Başlangıç saati bitiş saatinden küçük olmalı.")
                    .When(w => IsHHmm(w.StartTime) && IsHHmm(w.EndTime));

                // 1 saatlik slot kontrolü ve minimum/maksimum saat kontrolleri kaldırıldı
            });


    }
    private static bool TryParseHHmm(string? s, out TimeSpan t)
    {
        t = default;

        if (string.IsNullOrWhiteSpace(s))
            return false;

        // "HH:mm" formatında DateTime olarak parse et
        if (!DateTime.TryParseExact(
                s,
                "HH:mm",                        // tam mesajda yazdığın format
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
        {
            return false;
        }

        t = dt.TimeOfDay;  // 09:00 -> 09:00 TimeSpan
        return true;
    }
    private static bool IsHHmm(string? s) => TryParseHHmm(s, out _);

}
