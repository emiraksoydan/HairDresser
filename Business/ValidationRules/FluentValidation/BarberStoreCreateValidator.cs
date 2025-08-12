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
    public class BarberStoreCreateDtoValidator : AbstractValidator<BarberStoreCreateDto>
    {
        public BarberStoreCreateDtoValidator()
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
                c.RuleFor(x => x.Type)
                    .Must(type => type == ChairMode.Name || type == ChairMode.Barber).WithMessage("Geçersiz koltuk tipi");
                c.When(x => x.Type == ChairMode.Name, () =>
                {
                    c.RuleFor(x => x.Name)
                        .NotEmpty().WithMessage("Koltuk ismi zorunludur");
                });
                c.When(x => x.Type == ChairMode.Barber, () =>
                {
                    c.RuleFor(x => x.ManualBarberTempId)
                        .NotEmpty().WithMessage("Berber atanması zorunludur");
                });
            });
            RuleFor(x => x.Chairs)
             .Custom((chairs, context) =>
             {
                 if (chairs == null) return;

                 var ids = chairs
                     .Where(c => c.Type == ChairMode.Barber && c.ManualBarberTempId.HasValue)
                     .Select(c => c.ManualBarberTempId!.Value)
                     .ToList();

                 var duplicates = ids
                     .GroupBy(id => id)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key)
                     .ToList();

                 if (duplicates.Any())
                 {
                     context.AddFailure("Chairs", "Aynı manuel berber birden fazla koltuğa atanamaz.");


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

            RuleFor(x => x.Address)
    .NotNull().WithMessage("Adres bilgisi zorunludur")
    .ChildRules(address =>
    {
        address.RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("Adres yazısı zorunludur");
        address.RuleFor(x => x.Latitude)
            .NotNull().WithMessage("Enlem (latitude) bilgisi zorunludur");

        address.RuleFor(x => x.Longitude)
            .NotNull().WithMessage("Boylam (longitude) bilgisi zorunludur");
    });

        }
    }
}
