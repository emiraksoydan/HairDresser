using Business.Abstract;
using Business.Concrete;
using Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BadgeController(BadgeService badgeService) : ControllerBase
    {
        /// <summary>
        /// Gets all badge counts for the authenticated user
        /// Returns notification unread count, chat unread count, and per-thread unread counts
        /// </summary>
        /// <returns>Badge counts</returns>
        [HttpGet]
        public async Task<IActionResult> GetBadgeCounts()
        {
            var userId = User.GetUserIdOrThrow();
            var badgeCounts = await badgeService.GetBadgeCountsAsync(userId);

            return Ok(new
            {
                success = true,
                data = badgeCounts
            });
        }
    }
}
