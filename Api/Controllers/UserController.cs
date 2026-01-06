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
    public class UserController(IUserService userService, IPushNotificationService pushNotificationService) : ControllerBase
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

        /// <summary>
        /// Register FCM token for push notifications
        /// </summary>
        [HttpPost("register-fcm-token")]
        public async Task<IActionResult> RegisterFcmToken([FromBody] RegisterFcmTokenDto dto)
        {
            var result = await pushNotificationService.RegisterFcmTokenAsync(CurrentUserId, dto.FcmToken, dto.DeviceId, dto.Platform);
            return result ? Ok(new { success = true, message = "FCM token registered successfully" }) 
                         : BadRequest(new { success = false, message = "Failed to register FCM token" });
        }

        /// <summary>
        /// Unregister FCM token (logout, token refresh, etc.)
        /// </summary>
        [HttpPost("unregister-fcm-token")]
        public async Task<IActionResult> UnregisterFcmToken([FromBody] UnregisterFcmTokenDto dto)
        {
            var result = await pushNotificationService.UnregisterFcmTokenAsync(CurrentUserId, dto.FcmToken);
            return result ? Ok(new { success = true, message = "FCM token unregistered successfully" }) 
                         : BadRequest(new { success = false, message = "Failed to unregister FCM token" });
        }
    }

    public class RegisterFcmTokenDto
    {
        public string FcmToken { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? Platform { get; set; }
    }

    public class UnregisterFcmTokenDto
    {
        public string FcmToken { get; set; } = string.Empty;
    }
}
