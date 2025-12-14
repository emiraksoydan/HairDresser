using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FavoriteController : ControllerBase
    {
        private readonly IFavoriteService _favoriteService;

        public FavoriteController(IFavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] ToggleFavoriteDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _favoriteService.ToggleFavoriteAsync(userId, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("check/{targetId}")]
        public async Task<IActionResult> IsFavorite(Guid targetId)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _favoriteService.IsFavoriteAsync(userId, targetId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("my-favorites")]
        public async Task<IActionResult> GetMyFavorites()
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _favoriteService.GetMyFavoritesAsync(userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{targetId}")]
        public async Task<IActionResult> Remove(Guid targetId)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _favoriteService.RemoveFavoriteAsync(userId, targetId);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
