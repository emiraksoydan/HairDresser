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
            .NotEmpty().WithMessage("İşletme adı zorunludur.");

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

        RuleFor(x => x.TaxDocumentFilePath)
            .NotEmpty().WithMessage("Vergi levhası zorunludur.");

        // PricingValue koşullu
        When(x => x.PricingType == PricingType.Rent, () =>
        {
            RuleFor(x => x.PricingValue)
                .NotNull().WithMessage("Fiyat girilmelidir.")
                .GreaterThan(0).WithMessage("Fiyat 0'dan büyük olmalıdır.");
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
            .NotEmpty().WithMessage("En az bir koltuk eklenmelidir.");

        RuleForEach(x => x.Chairs).ChildRules(c =>
        {
            // XOR: ya isim var ya BarberId var (ikisinden tam biri dolu)
            c.RuleFor(ch => new { ch.Name, ch.BarberId })
             .Must(v => string.IsNullOrWhiteSpace(v.Name) ^ string.IsNullOrWhiteSpace(v.BarberId))
             .WithMessage("Koltuk ya isimli olmalı ya da bir berbere atanmalı; ikisi birden veya ikisi de boş olamaz.");

            // İsimli koltuk ise isim zorunlu
            c.When(ch => !string.IsNullOrWhiteSpace(ch.Name), () =>
            {
                c.RuleFor(ch => ch.Name)
                 .NotEmpty().WithMessage("Koltuk ismi zorunludur.");
            });

            // Berbere atanmışsa BarberId format kontrolü
            c.When(ch => !string.IsNullOrWhiteSpace(ch.BarberId), () =>
            {
                c.RuleFor(ch => ch.BarberId!)
                 .Must(s => Guid.TryParse(s, out var g) && g != Guid.Empty)
                 .WithMessage("Geçerli bir Berber Id giriniz.");
            });
        });

        // Offerings
        RuleFor(x => x.Offerings)
            .NotEmpty().WithMessage("En az bir hizmet girilmelidir.");

        RuleForEach(x => x.Offerings).ChildRules(o =>
        {
            o.RuleFor(v => v.ServiceName)
             .NotEmpty().WithMessage("Hizmet adı boş olamaz.");

            o.RuleFor(v => v.Price)
             .GreaterThan(0).WithMessage("Hizmet fiyatı 0'dan büyük olmalıdır.");
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

                // 6–18 saat aralığı
                c.RuleFor(w => w)
                    .Must(w =>
                    {
                        TryParseHHmm(w.StartTime, out var s);
                        TryParseHHmm(w.EndTime, out var e);
                        var hours = (e - s).TotalHours;
                        return hours >= 6 && hours <= 18;
                    })
                    .WithMessage("Çalışma süresi en az 6 ve en fazla 18 saat olmalı.")
                    .When(w =>
                    {
                        if (!IsHHmm(w.StartTime) || !IsHHmm(w.EndTime)) return false;
                        TryParseHHmm(w.StartTime, out var s);
                        TryParseHHmm(w.EndTime, out var e);
                        return s < e;
                    });
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
