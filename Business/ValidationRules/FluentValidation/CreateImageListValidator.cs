using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateImageListValidator : AbstractValidator<List<CreateImageDto>>
    {
        private readonly IImageDal _imageDal;

        public CreateImageListValidator(IImageDal imageDal)
        {
            _imageDal = imageDal;

            RuleFor(x => x)
                .NotNull().WithMessage("Resim listesi boş olamaz")
                .Must(list => list.Count > 0).WithMessage("En az bir resim eklenmelidir");

            RuleForEach(x => x).SetValidator(new CreateImageDtoValidator(_imageDal));

            // Grup bazında image count kontrolü
            RuleFor(x => x)
                .MustAsync(async (list, cancellation) =>
                {
                    if (list == null || list.Count == 0)
                        return true;

                    // Her owner için gruplayıp kontrol et
                    var grouped = list.GroupBy(x => new { x.ImageOwnerId, x.OwnerType });

                    foreach (var group in grouped)
                    {
                        if (!group.Key.ImageOwnerId.HasValue)
                            continue;

                        var maxImages = group.Key.OwnerType switch
                        {
                            ImageOwnerType.User => 1,
                            ImageOwnerType.ManuelBarber => 1,
                            ImageOwnerType.Store => 3,
                            ImageOwnerType.FreeBarber => 3,
                            _ => 1
                        };

                        var existingCount = await _imageDal.CountAsync(x =>
                            x.ImageOwnerId == group.Key.ImageOwnerId.Value &&
                            x.OwnerType == group.Key.OwnerType);

                        var totalCount = existingCount + group.Count();

                        if (totalCount > maxImages)
                            return false;
                    }

                    return true;
                })
                .WithMessage(list =>
                {
                    if (list == null || list.Count == 0)
                        return "Resim listesi boş";

                    var firstItem = list.First();
                    var maxImages = firstItem.OwnerType switch
                    {
                        ImageOwnerType.User => 1,
                        ImageOwnerType.ManuelBarber => 1,
                        ImageOwnerType.Store => 3,
                        ImageOwnerType.FreeBarber => 3,
                        _ => 1
                    };

                    var ownerTypeText = firstItem.OwnerType switch
                    {
                        ImageOwnerType.User => "Kullanıcı",
                        ImageOwnerType.ManuelBarber => "Manuel berber",
                        ImageOwnerType.Store => "Dükkan",
                        ImageOwnerType.FreeBarber => "Serbest berber",
                        _ => "Sahip"
                    };

                    return $"{ownerTypeText} için toplam en fazla {maxImages} resim eklenebilir";
                });
        }
    }
}
