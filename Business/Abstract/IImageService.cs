using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;


namespace Business.Abstract
{
    public interface IImageService
    {
        Task<IResult> AddAsync(CreateImageDto createImageDto);
        Task<IResult> AddRangeAsync(List<CreateImageDto> list);
        Task<IResult> UpdateAsync(UpdateImageDto updateImageDto);
        Task<IResult> UpdateRangeAsync(List<UpdateImageDto> list);
        Task<IResult> DeleteAsync(Guid id);

        Task<IDataResult<ImageGetDto>> GetImage(Guid id);

        /// <summary>
        /// Upload single image to file storage
        /// </summary>
        Task<IDataResult<string>> UploadImageAsync(IFormFile file, ImageOwnerType ownerType, Guid ownerId, bool updateProfileImage = true);

        /// <summary>
        /// Upload multiple images to file storage
        /// </summary>
        Task<IDataResult<List<string>>> UploadImagesAsync(List<IFormFile> files, ImageOwnerType ownerType, Guid ownerId);

        /// <summary>
        /// Get all images by owner
        /// </summary>
        Task<IDataResult<List<ImageGetDto>>> GetImagesByOwnerAsync(Guid ownerId, ImageOwnerType ownerType);

        /// <summary>
        /// Updates an existing image blob without creating a new one
        /// </summary>
        Task<IResult> UpdateImageBlobAsync(Guid imageId, IFormFile file);
    }
}
