using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class FavoriteController : BaseApiController
    {
        private readonly IFavoriteService _favoriteService;

        public FavoriteController(IFavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] ToggleFavoriteDto dto)
        {
            return await HandleUserDataOperation(userId => _favoriteService.ToggleFavoriteAsync(userId, dto));
        }

        [HttpGet("check/{targetId}")]
        public async Task<IActionResult> IsFavorite(Guid targetId)
        {
            return await HandleUserDataOperation(userId => _favoriteService.IsFavoriteAsync(userId, targetId));
        }

        [HttpGet("my-favorites")]
        public async Task<IActionResult> GetMyFavorites()
        {
            return await HandleUserDataOperation(userId => _favoriteService.GetMyFavoritesAsync(userId));
        }

        [HttpDelete("{targetId}")]
        public async Task<IActionResult> Remove(Guid targetId)
        {
            return await HandleUserDataOperation(userId => _favoriteService.RemoveFavoriteAsync(userId, targetId));
        }
    }
}
