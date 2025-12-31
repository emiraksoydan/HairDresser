using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController(IUserService userService) : ControllerBase
    {
        private Guid CurrentUserId => User.GetUserIdOrThrow();
        /// <summary>
        /// Update current user's profile information
        /// </summary>
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserDto dto)
        {
            var result = await userService.UpdateProfile(dto, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Get current user's profile information
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var result = await userService.GetMe(CurrentUserId);
            return result.Success ? Ok(result) : NotFound(result);
        }
    }
}
