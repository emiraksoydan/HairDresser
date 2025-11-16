using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class BarberStoreUpdateDtoValidator : AbstractValidator<BarberStoreUpdateDto>
    {
        public BarberStoreUpdateDtoValidator()
        {
            RuleFor(x => x.StoreName)
                .NotEmpty().WithMessage("İşletme adı zorunludur");

            RuleFor(x => x.Type)
                .NotNull().WithMessage("İşletme türü zorunludur")
                .IsInEnum().WithMessage("Geçerli bir işletme türü seçilmelidir");
            RuleFor(x => x.Offerings)
                 .NotNull().WithMessage("Hizmet listesi zorunludur")
                 .Must(x => x.Count > 0).WithMessage("En az bir hizmet girilmelidir");
            RuleForEach(x => x.Offerings).ChildRules(o =>
            {
                o.RuleFor(x => x.ServiceName)
                    .NotEmpty().WithMessage("Hizmet adı boş olamaz");

                o.RuleFor(x => x.Price)
                    .GreaterThan(0).WithMessage("Hizmet fiyatı 0'dan büyük olmalıdır");
            });

            RuleForEach(x => x.ManualBarbers).ChildRules(b =>
            {
                b.RuleFor(x => x.FirstName)
                    .NotEmpty().WithMessage("Berber adı zorunludur");
            });
            RuleFor(x => x.Chairs)
                .NotNull().WithMessage("Koltuk listesi boş olamaz")
                .Must(x => x.Count > 0).WithMessage("En az bir koltuk eklenmelidir");
            RuleForEach(x => x.Chairs).ChildRules(c =>
            {

                c.When(x => x.Name != null, () =>
                {
                    c.RuleFor(x => x.Name)
                        .NotEmpty()
                        .WithMessage("Koltuk ismi boş olamaz");
                });

                c.When(x => x.Name == null, () =>
                {
                    c.RuleFor(x => x.ManualBarberTempId)
                        .NotNull()
                        .WithMessage("Lütfen berber koltuk türüne berber atayınız");
                });
            });
            RuleFor(x => x.Chairs).Custom((chairs, ctx) =>
            {
                if (chairs == null) return;

                var dupeIds = chairs
                    .Where(c => string.IsNullOrWhiteSpace(c.Name) && c.ManualBarberTempId.HasValue)
                    .GroupBy(c => c.ManualBarberTempId!.Value)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (dupeIds.Any())
                {
                    ctx.AddFailure("Chairs", "Aynı manuel berber birden fazla koltuğa atanamaz.");

                }
            });

            RuleFor(x => x.PricingType)
                      .NotEmpty()
                      .WithMessage("Koltuk fiyat hizmeti seçilmelidir");

            When(x => x.PricingType == "rent", () =>
            {
                RuleFor(x => x.PricingValue).NotNull().WithMessage("Fiyat girilmelidir").GreaterThan(0).WithMessage("Fiyat 0 dan büyük olmalıdır");
            });

            When(x => x.PricingType == "percent", () =>
            {
                RuleFor(x => x.PricingValue).NotNull().WithMessage("Yüzdelik seçilmelidir");
            });


            RuleForEach(x => x.WorkingHours)
           .Where(x => !x.IsClosed)
           .Must(x =>
           {
               return TimeSpan.TryParse(x.StartTime, out var start)
                   && TimeSpan.TryParse(x.EndTime, out var end)
                   && start < end;
           })
           .WithMessage("başlangıç saati bitiş saatinden büyük veya eşit olmamalı");

       

        }
    }
}
