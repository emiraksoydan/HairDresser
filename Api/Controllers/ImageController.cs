using Business.Abstract;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController(IImageService imageService) : ControllerBase
    {
        /// <summary>
        /// Upload single image to Azure Blob Storage
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile file, [FromForm] ImageOwnerType ownerType, [FromForm] Guid ownerId)
        {
            // Validate ownerId is not empty
            if (ownerId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Resim sahibi ID'si boş olamaz" });
            }
            
            var result = await imageService.UploadImageAsync(file, ownerType, ownerId);
            return result.Success ? Ok(result) : BadRequest(result);
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
                return BadRequest(new { success = false, message = "Resim sahibi ID'si boş olamaz" });
            }
            
            var result = await imageService.UploadImagesAsync(files, ownerType, ownerId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Get all images by owner
        /// </summary>
        [HttpGet("owner/{ownerId}")]
        public async Task<IActionResult> GetImagesByOwner(Guid ownerId, [FromQuery] ImageOwnerType ownerType)
        {
            var result = await imageService.GetImagesByOwnerAsync(ownerId, ownerType);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Delete image by ID
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(Guid id)
        {
            var result = await imageService.DeleteAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Get image by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetImage(Guid id)
        {
            var result = await imageService.GetImage(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Update existing image blob without creating a new one
        /// </summary>
        [HttpPut("update-blob/{imageId}")]
        public async Task<IActionResult> UpdateImageBlob(Guid imageId, [FromForm] IFormFile file)
        {
            if (imageId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Resim ID'si boş olamaz" });
            }

            var result = await imageService.UpdateImageBlobAsync(imageId, file);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
