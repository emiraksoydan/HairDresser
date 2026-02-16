using Business.Abstract;
using Business.Resources;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ImageController : BaseApiController
    {
        private readonly IImageService _imageService;

        public ImageController(IImageService imageService)
        {
            _imageService = imageService;
        }

        /// <summary>
        /// Upload single image to file storage
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] ImageUploadRequest request, [FromQuery] bool? isProfileImage = null)
        {
            if (request.OwnerId == Guid.Empty)
                return BadRequest(Messages.ImageOwnerIdRequired);

            // Query string'den gelen değer öncelikli, yoksa form'dan gelen değer kullanılır
            var updateProfileImage = isProfileImage ?? request.IsProfileImage;

            return await HandleDataResultAsync(
                _imageService.UploadImageAsync(
                    request.File,
                    request.OwnerType,
                    request.OwnerId,
                    updateProfileImage));
        }

        /// <summary>
        /// Upload multiple images to file storage
        /// </summary>
        [HttpPost("upload-multiple")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages([FromForm] ImageMultiUploadRequestDto request)
        {
            if (request.OwnerId == Guid.Empty)
                return BadRequest(Messages.ImageOwnerIdRequired);

            return await HandleDataResultAsync(
                _imageService.UploadImagesAsync(
                    request.Files,
                    request.OwnerType,
                    request.OwnerId));
        }

        /// <summary>
        /// Get all images by owner
        /// </summary>
        [HttpGet("owner/{ownerId}")]
        public async Task<IActionResult> GetImagesByOwner(
            Guid ownerId,
            [FromQuery] ImageOwnerType ownerType)
        {
            return await HandleDataResultAsync(
                _imageService.GetImagesByOwnerAsync(ownerId, ownerType));
        }

        /// <summary>
        /// Delete image by ID
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(Guid id)
        {
            return await HandleResultAsync(_imageService.DeleteAsync(id));
        }

        /// <summary>
        /// Get image by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetImage(Guid id)
        {
            return await HandleDataResultAsync(_imageService.GetImage(id));
        }

        /// <summary>
        /// Update existing image blob without creating a new one
        /// </summary>
        [HttpPut("update-blob")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateImageBlob(
            [FromForm] UpdateImageBlobRequestDto request)
        {
            if (request.ImageId == Guid.Empty)
                return BadRequest(Messages.ImageIdRequired);

            return await HandleResultAsync(
                _imageService.UpdateImageBlobAsync(
                    request.ImageId,
                    request.File));
        }

    }
}
