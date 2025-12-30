using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateImageDtoValidator : AbstractValidator<CreateImageDto>
    {
        private readonly IImageDal _imageDal;

        public CreateImageDtoValidator(IImageDal imageDal)
        {
            _imageDal = imageDal;

            RuleFor(x => x.ImageUrl)
                .NotEmpty().WithMessage("Resim URL'i boş olamaz")
                .MaximumLength(2000).WithMessage("Resim URL'i en fazla 2000 karakter olabilir");

            RuleFor(x => x.OwnerType)
                .IsInEnum().WithMessage("Geçerli bir sahip türü seçilmelidir");

            RuleFor(x => x.ImageOwnerId)
                .NotEmpty().WithMessage("Resim sahibi ID'si boş olamaz");

            // Image count validation
            RuleFor(x => x)
                .MustAsync(async (dto, cancellation) =>
                {
                    if (!dto.ImageOwnerId.HasValue)
                        return false;

                    var maxImages = dto.OwnerType switch
                    {
                        ImageOwnerType.User => 1,
                        ImageOwnerType.ManuelBarber => 1,
                        ImageOwnerType.Store => 3,
                        ImageOwnerType.FreeBarber => 3,
                        _ => 1
                    };

                    var existingCount = await _imageDal.CountAsync(x =>
                        x.ImageOwnerId == dto.ImageOwnerId.Value &&
                        x.OwnerType == dto.OwnerType);

                    return existingCount < maxImages;
                })
                .WithMessage(dto =>
                {
                    var maxImages = dto.OwnerType switch
                    {
                        ImageOwnerType.User => 1,
                        ImageOwnerType.ManuelBarber => 1,
                        ImageOwnerType.Store => 3,
                        ImageOwnerType.FreeBarber => 3,
                        _ => 1
                    };

                    var ownerTypeText = dto.OwnerType switch
                    {
                        ImageOwnerType.User => "Kullanıcı",
                        ImageOwnerType.ManuelBarber => "Manuel berber",
                        ImageOwnerType.Store => "Dükkan",
                        ImageOwnerType.FreeBarber => "Serbest berber",
                        _ => "Sahip"
                    };

                    return $"{ownerTypeText} için en fazla {maxImages} resim eklenebilir";
                });
        }
    }
}
