using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class BlockedController : BaseApiController
    {
        private readonly IBlockedService _blockedService;

        public BlockedController(IBlockedService blockedService)
        {
            _blockedService = blockedService;
        }

        /// <summary>
        /// Kullanıcıyı engelle
        /// </summary>
        [HttpPost("block")]
        public async Task<IActionResult> Block([FromBody] CreateBlockedDto dto)
        {
            return await HandleUserDataOperation(userId => _blockedService.BlockUserAsync(userId, dto));
        }

        /// <summary>
        /// Engeli kaldır
        /// </summary>
        [HttpPost("unblock")]
        public async Task<IActionResult> Unblock([FromBody] UnblockDto dto)
        {
            return await HandleUserDataOperation(userId => _blockedService.UnblockUserAsync(userId, dto.BlockedToUserId));
        }

        /// <summary>
        /// Engellenen kullanıcıları getir
        /// </summary>
        [HttpGet("my-blocked")]
        public async Task<IActionResult> GetMyBlockedUsers()
        {
            return await HandleUserDataOperation(userId => _blockedService.GetMyBlockedUsersAsync(userId));
        }

        /// <summary>
        /// İki kullanıcı arasındaki engelleme durumunu kontrol et
        /// </summary>
        [HttpGet("status/{otherUserId}")]
        public async Task<IActionResult> GetBlockStatus(Guid otherUserId)
        {
            return await HandleUserDataOperation(userId => _blockedService.GetBlockStatusAsync(userId, otherUserId));
        }

        /// <summary>
        /// Kullanıcının tüm engellediği ve engellendiği ID'leri getir (filtreleme için)
        /// </summary>
        [HttpGet("all-blocked-ids")]
        public async Task<IActionResult> GetAllBlockedIds()
        {
            return await HandleUserDataOperation(userId => _blockedService.GetAllBlockedUserIdsAsync(userId));
        }
    }
}
