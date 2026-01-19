using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using Entities.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;


namespace Business.Concrete
{
    public class ImageManager(IImageDal _imageDal, IBlobStorageService _blobStorageService, IUserDal _userDal, DataAccess.Abstract.IBarberStoreDal _barberStoreDal, DataAccess.Abstract.IFreeBarberDal _freeBarberDal, IRealTimePublisher _realTimePublisher) : IImageService
    {
        public async Task<IResult> AddAsync(CreateImageDto createImageDto)
        {
            var getImage = createImageDto.Adapt<Image>();
            await _imageDal.Add(getImage);
            return new SuccessResult();
        }

        public async Task<IResult> AddRangeAsync(List<CreateImageDto> list)
        {
            var imageEntities = list.Adapt<List<Image>>();

            await _imageDal.AddRange(imageEntities);
            return new SuccessResult();
        }

        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteAsync(Guid id)
        {
            var getImage = await _imageDal.Get(i=>i.Id == id);
            if (getImage == null)
                return new ErrorResult("Resim bulunamadı.");

            // Delete from Azure Blob Storage
            if (!string.IsNullOrEmpty(getImage.ImageUrl))
            {
                await _blobStorageService.DeleteAsync(getImage.ImageUrl);
            }

            await _imageDal.Remove(getImage);
            return new SuccessResult();
        }

        public async Task<IDataResult<ImageGetDto>> GetImage(Guid id)
        {
            var image = await _imageDal.Get(x => x.Id == id);
            if (image == null)
                return new ErrorDataResult<ImageGetDto>("Resim bulunamadı.");

            var dto = image.Adapt<ImageGetDto>();

            return new SuccessDataResult<ImageGetDto>(dto);
        }

        [ValidationAspect(typeof(UpdateImageDtoValidator))]
        public async Task<IResult> UpdateAsync(UpdateImageDto updateImageDto)
        {
            var entity = await _imageDal.Get(i => i.Id == updateImageDto.Id);
            if (entity == null)
                return new ErrorResult("Resim bulunamadı.");

            // ÖNEMLİ: UpdateImageDto sadece metadata güncellemesi için kullanılır
            // ImageUrl değişmişse, bu yeni blob'un URL'i değil, mevcut blob'un URL'i korunmalı
            // Mevcut blob güncellemesi için UpdateImageBlobAsync kullanılmalı
            // Burada sadece ImageOwnerId ve OwnerType güncelle (ImageUrl'i koru)
            var oldImageUrl = entity.ImageUrl;
            
            // ImageUrl'i koru, sadece diğer alanları güncelle
            entity.ImageOwnerId = updateImageDto.ImageOwnerId;
            entity.OwnerType = updateImageDto.OwnerType ?? entity.OwnerType;
            entity.UpdatedAt = DateTime.UtcNow;
            
            // ImageUrl değişmişse uyar (ama değiştirme - mevcut blob korunmalı)
            if (!string.IsNullOrEmpty(oldImageUrl) && 
                !string.IsNullOrEmpty(updateImageDto.ImageUrl) && 
                oldImageUrl != updateImageDto.ImageUrl)
            {
                // ÖNEMLİ: ImageUrl değişmişse, yeni blob oluşturulmamalı
                // Mevcut blob korunmalı, blob güncellemesi için UpdateImageBlobAsync kullanılmalı
                // Burada ImageUrl'i değiştirmiyoruz, mevcut blob'u koruyoruz
            }

            await _imageDal.Update(entity);
            return new SuccessResult();
        }

        public async Task<IResult> UpdateRangeAsync(List<UpdateImageDto> list)
        {
            if (list == null || list.Count == 0)
                return new SuccessResult();
            var updateDtos = list
                .Where(d => d.Id != Guid.Empty)
                .ToList();
            var newDtos = list
                .Where(d => d.Id == Guid.Empty)
                .ToList();

            List<Image> existingImages = new();
            if (updateDtos.Any())
            {
                var updateIds = updateDtos.Select(d => d.Id).ToList();
                existingImages = await _imageDal.GetAll(x => updateIds.Contains(x.Id));
            }
            var imageDict = existingImages.ToDictionary(x => x.Id);
            foreach (var dto in updateDtos)
            {
                if (!imageDict.TryGetValue(dto.Id, out var entity))
                    continue;

                // ÖNEMLİ: ImageUrl değişmişse, mevcut blob'u koru (yeni blob oluşturulmamalı)
                // Sadece ImageOwnerId ve OwnerType güncelle
                entity.ImageOwnerId = dto.ImageOwnerId;
                entity.OwnerType = dto.OwnerType ?? entity.OwnerType;
                entity.UpdatedAt = DateTime.UtcNow;
                
                // ImageUrl'i koru - mevcut blob güncellemesi için UpdateImageBlobAsync kullanılmalı
                // ImageUrl değişmişse uyar ama değiştirme
                if (!string.IsNullOrEmpty(entity.ImageUrl) && 
                    !string.IsNullOrEmpty(dto.ImageUrl) && 
                    entity.ImageUrl != dto.ImageUrl)
                {
                    // ÖNEMLİ: ImageUrl değişmişse, yeni blob oluşturulmamalı
                    // Mevcut blob korunmalı, blob güncellemesi için UpdateImageBlobAsync kullanılmalı
                    // Burada ImageUrl'i değiştirmiyoruz, mevcut blob'u koruyoruz
                }
            }
            if (existingImages.Any())
            {
                await _imageDal.UpdateRange(existingImages);
            }
            if (newDtos.Any())
            {
                var newEntities = newDtos.Adapt<List<Image>>();
                foreach (var entity in newEntities.Where(x=>x.Id == Guid.Empty))
                {
                    entity.Id = Guid.NewGuid();
                    entity.CreatedAt = DateTime.UtcNow;
                }

                await _imageDal.AddRange(newEntities);
            }
            return new SuccessResult();
        }

        [LogAspect]
        public async Task<IDataResult<string>> UploadImageAsync(Microsoft.AspNetCore.Http.IFormFile file, Entities.Concrete.Enums.ImageOwnerType ownerType, Guid ownerId, bool updateProfileImage = true)
        {
            // ÖNEMLİ: Bu metod sadece TEK RESİM için kullanılır:
            // - User profile image (profil fotoğrafı)
            // - Store tax document (vergi belgesi) - galeri resimlerinden bağımsız
            // - FreeBarber certificate (sertifika) - galeri resimlerinden bağımsız
            // - ManuelBarber image (manuel berber fotoğrafı)
            //
            // Galeri resimleri (Store/FreeBarber için max 3) UploadImagesAsync ile yüklenir
            // Bu yüzden burada maxImages kontrolü YOK - belgeler ve sertifikalar galeri limitinden bağımsız

            // Get container name based on owner type
            var containerName = ownerType switch
            {
                Entities.Concrete.Enums.ImageOwnerType.User => "user-images",     // For profile images only
                Entities.Concrete.Enums.ImageOwnerType.Store => "store-images",
                Entities.Concrete.Enums.ImageOwnerType.FreeBarber => "freebarber-images",
                Entities.Concrete.Enums.ImageOwnerType.ManuelBarber => "manuelbarber-images",
                _ => throw new ArgumentOutOfRangeException(nameof(ownerType), ownerType, "Geçersiz resim sahibi tipi")
            };

            // Upload to Azure Blob Storage
            var imageUrl = await _blobStorageService.UploadAsync(file, containerName);
            
            // Cache busting için timestamp ekle
            var urlWithTimestamp = $"{imageUrl}?t={DateTime.UtcNow.Ticks}";

            // Save to database
            var image = new Image
            {
                Id = Guid.NewGuid(),
                ImageUrl = urlWithTimestamp,
                OwnerType = ownerType,
                ImageOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _imageDal.Add(image);

            // If this is a User profile image, update the User's ImageId
            if (updateProfileImage && ownerType == Entities.Concrete.Enums.ImageOwnerType.User)
            {
                var user = await _userDal.Get(u => u.Id == ownerId);
                if (user != null)
                {
                    // If user already has a profile image, we keep the old one (don't delete)
                    // Just update the ImageId to point to the new image
                    user.ImageId = image.Id;
                    await _userDal.Update(user);
                }
                
                // SignalR push - tüm kullanıcılara yeni profil fotoğrafı bildir
                try
                {
                    await _realTimePublisher.PushImageUpdatedAsync(ownerId, image.Id, urlWithTimestamp);
                }
                catch
                {
                    // SignalR failure should not break the upload
                }
            }

            // Return the Image ID, not the URL
            return new SuccessDataResult<string>(image.Id.ToString(), "Resim başarıyla yüklendi.");
        }

        [LogAspect(logParameters: true, logReturnValue: true)]
        public async Task<IDataResult<List<string>>> UploadImagesAsync(List<Microsoft.AspNetCore.Http.IFormFile> files, Entities.Concrete.Enums.ImageOwnerType ownerType, Guid ownerId)
        {
            // ÖNEMLİ: Bu metod SADECE PANEL GALERİ RESİMLERİ için kullanılır (Store/FreeBarber - max 3)
            // Tax document, certificate, profile image gibi belgeler UploadImageAsync ile yüklenir
            
            // Sadece dosya sayısı kontrolü yap (request başına max 3 dosya - panel limiti)
            if (files.Count > 3)
            {
                return new ErrorDataResult<List<string>>(
                    $"Tek seferde en fazla 3 resim yüklenebilir. Gönderilen: {files.Count}");
            }

            // Store ve FreeBarber için mevcut GALERİ resim sayısını kontrol et
            // ÖNEMLİ: Tax document ve certificate galeri resimlerinden bağımsızdır
            // Bu yüzden sadece galeri resimlerini saymalıyız (certificate/tax document hariç)
            if (ownerId != Guid.Empty &&
                (ownerType == Entities.Concrete.Enums.ImageOwnerType.Store ||
                 ownerType == Entities.Concrete.Enums.ImageOwnerType.FreeBarber))
            {
                var maxImages = ownerType switch
                {
                    Entities.Concrete.Enums.ImageOwnerType.Store => 3,
                    Entities.Concrete.Enums.ImageOwnerType.FreeBarber => 3,
                    _ => 1
                };

                // TÜM resimleri say
                var allImagesCount = await _imageDal.CountAsync(x =>
                    x.ImageOwnerId == ownerId &&
                    x.OwnerType == ownerType);

                // Tax document veya certificate'ı hariç tut - sadece galeri resimlerini say
                int galleryImagesCount = allImagesCount;
                
                if (ownerType == Entities.Concrete.Enums.ImageOwnerType.Store)
                {
                    var store = await _barberStoreDal.Get(s => s.Id == ownerId);
                    if (store != null && store.TaxDocumentImageId.HasValue)
                    {
                        // Tax document'ı hariç tut - galeri resimlerini say
                        galleryImagesCount = await _imageDal.CountAsync(x =>
                            x.ImageOwnerId == ownerId &&
                            x.OwnerType == ownerType &&
                            x.Id != store.TaxDocumentImageId.Value);
                    }
                }
                else if (ownerType == Entities.Concrete.Enums.ImageOwnerType.FreeBarber)
                {
                    var freeBarber = await _freeBarberDal.Get(fb => fb.Id == ownerId);
                    if (freeBarber != null && freeBarber.BarberCertificateImageId.HasValue)
                    {
                        // Certificate'ı hariç tut - galeri resimlerini say
                        galleryImagesCount = await _imageDal.CountAsync(x =>
                            x.ImageOwnerId == ownerId &&
                            x.OwnerType == ownerType &&
                            x.Id != freeBarber.BarberCertificateImageId.Value);
                    }
                }

                var totalCount = galleryImagesCount + files.Count;

                if (totalCount > maxImages)
                {
                    var ownerTypeText = ownerType switch
                    {
                        Entities.Concrete.Enums.ImageOwnerType.Store => "Dükkan",
                        Entities.Concrete.Enums.ImageOwnerType.FreeBarber => "Serbest berber",
                        _ => "Sahip"
                    };

                    return new ErrorDataResult<List<string>>(
                        $"{ownerTypeText} için en fazla {maxImages} galeri resmi eklenebilir. Mevcut galeri resim sayısı: {galleryImagesCount}, eklenmek istenen: {files.Count}, toplam: {totalCount}");
                }
            }

            // Get container name based on owner type
            var containerName = ownerType switch
            {
                Entities.Concrete.Enums.ImageOwnerType.User => "user-images",     // For profile images only
                Entities.Concrete.Enums.ImageOwnerType.Store => "store-images",
                Entities.Concrete.Enums.ImageOwnerType.FreeBarber => "freebarber-images",
                Entities.Concrete.Enums.ImageOwnerType.ManuelBarber => "manuelbarber-images",
                _ => throw new ArgumentOutOfRangeException(nameof(ownerType), ownerType, "Geçersiz resim sahibi tipi")
            };

            // Upload all files to Azure Blob Storage
            var imageUrls = await _blobStorageService.UploadMultipleAsync(files, containerName);

            // Save all to database
            var images = imageUrls.Select(url => new Image
            {
                Id = Guid.NewGuid(),
                ImageUrl = url,
                OwnerType = ownerType,
                ImageOwnerId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await _imageDal.AddRange(images);

            return new SuccessDataResult<List<string>>(imageUrls, $"{files.Count} resim başarıyla yüklendi.");
        }

        public async Task<IDataResult<List<ImageGetDto>>> GetImagesByOwnerAsync(Guid ownerId, Entities.Concrete.Enums.ImageOwnerType ownerType)
        {
            var images = await _imageDal.GetAll(x =>
                x.ImageOwnerId == ownerId &&
                x.OwnerType == ownerType);

            // En son eklenen image ilk sırada olsun (CreatedAt DESC)
            var orderedImages = images.OrderByDescending(i => i.CreatedAt).ToList();

            var dtos = orderedImages.Adapt<List<ImageGetDto>>();

            return new SuccessDataResult<List<ImageGetDto>>(dtos);
        }

        /// <summary>
        /// Updates an existing image blob without creating a new one
        /// Mevcut blob'un içeriğini günceller, yeni blob oluşturmaz
        /// </summary>
        public async Task<IResult> UpdateImageBlobAsync(Guid imageId, Microsoft.AspNetCore.Http.IFormFile file)
        {
            var entity = await _imageDal.Get(i => i.Id == imageId);
            if (entity == null)
                return new ErrorResult("Resim bulunamadı.");

            if (string.IsNullOrEmpty(entity.ImageUrl))
                return new ErrorResult("Resim URL'i bulunamadı.");

            // Mevcut blob'u güncelle (yeni blob oluşturma)
            var updatedUrl = await _blobStorageService.UpdateAsync(file, entity.ImageUrl);
            
            // ImageUrl'e cache busting için timestamp ekle
            var urlWithTimestamp = $"{updatedUrl}?t={DateTime.UtcNow.Ticks}";
            entity.ImageUrl = urlWithTimestamp;
            
            // UpdatedAt'i güncelle
            entity.UpdatedAt = DateTime.UtcNow;
            await _imageDal.Update(entity);

            // SignalR push - tüm kullanıcılara image güncellemesi bildir
            if (entity.OwnerType == Entities.Concrete.Enums.ImageOwnerType.User && entity.ImageOwnerId != Guid.Empty)
            {
                try
                {
                    await _realTimePublisher.PushImageUpdatedAsync(entity.ImageOwnerId, imageId, urlWithTimestamp);
                }
                catch
                {
                    // SignalR failure should not break the update
                }
            }

            return new SuccessResult("Resim başarıyla güncellendi.");
        }
    }
}
