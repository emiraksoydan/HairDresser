using Business.Abstract;
using Business.Resources;
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
        /// Upload single image to Azure Blob Storage
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile file, [FromForm] ImageOwnerType ownerType, [FromForm] Guid ownerId, [FromQuery] bool isProfileImage = true)
        {
            // Validate ownerId is not empty
            if (ownerId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = Messages.ImageOwnerIdRequired });
            }

            return await HandleDataResultAsync(_imageService.UploadImageAsync(file, ownerType, ownerId, isProfileImage));
        }

        /// <summary>
        /// Upload multiple images to Azure Blob Storage
        /// </summary>
        [HttpPost("upload-multiple")]
        public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files, [FromForm] ImageOwnerType ownerType, [FromForm] Guid ownerId)
        {
            // Validate ownerId is not empty
            if (ownerId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = Messages.ImageOwnerIdRequired });
            }

            return await HandleDataResultAsync(_imageService.UploadImagesAsync(files, ownerType, ownerId));
        }

        /// <summary>
        /// Get all images by owner
        /// </summary>
        [HttpGet("owner/{ownerId}")]
        public async Task<IActionResult> GetImagesByOwner(Guid ownerId, [FromQuery] ImageOwnerType ownerType)
        {
            return await HandleDataResultAsync(_imageService.GetImagesByOwnerAsync(ownerId, ownerType));
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
        [HttpPut("update-blob/{imageId}")]
        public async Task<IActionResult> UpdateImageBlob(Guid imageId, [FromForm] IFormFile file)
        {
            if (imageId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = Messages.ImageIdRequired });
            }

            return await HandleResultAsync(_imageService.UpdateImageBlobAsync(imageId, file));
        }
    }
}
