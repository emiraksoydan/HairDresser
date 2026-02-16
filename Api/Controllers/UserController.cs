using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class UserController : BaseApiController
    {
        private readonly IUserService _userService;
        private readonly IPushNotificationService _pushNotificationService;

        public UserController(IUserService userService, IPushNotificationService pushNotificationService)
        {
            _userService = userService;
            _pushNotificationService = pushNotificationService;
        }

        /// <summary>
        /// Update current user's profile information
        /// </summary>
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserDto dto)
        {
            return await HandleUserDataOperation(userId => _userService.UpdateProfile(dto, userId));
        }

        /// <summary>
        /// Get current user's profile information
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            return await HandleUserDataOperation(userId => _userService.GetMe(userId));
        }

        /// <summary>
        /// Telefon değişikliği için OTP gönder (yeni numaraya)
        /// </summary>
        [HttpPost("send-phone-change-otp")]
        public async Task<IActionResult> SendPhoneChangeOtp([FromBody] SendPhoneChangeOtpDto dto)
        {
            var result = await _userService.SendPhoneChangeOtpAsync(CurrentUserId, dto.NewPhone);
            return HandleResult(result);
        }

        /// <summary>
        /// OTP doğrulaması ile telefon numarasını güncelle
        /// </summary>
        [HttpPut("update-phone")]
        public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneDto dto)
        {
            return await HandleUserDataOperation(userId => _userService.UpdatePhoneAsync(userId, dto.NewPhone, dto.OtpCode));
        }

        /// <summary>
        /// Register FCM token for push notifications
        /// </summary>
        [HttpPost("register-fcm-token")]
        public async Task<IActionResult> RegisterFcmToken([FromBody] RegisterFcmTokenDto dto)
        {
            var result = await _pushNotificationService.RegisterFcmTokenAsync(CurrentUserId, dto.FcmToken, dto.DeviceId, dto.Platform);
            return result ? Ok(new { success = true, message = "FCM token registered successfully" })
                         : BadRequest(new { success = false, message = "Failed to register FCM token" });
        }

        /// <summary>
        /// Unregister FCM token (logout, token refresh, etc.)
        /// </summary>
        [HttpPost("unregister-fcm-token")]
        public async Task<IActionResult> UnregisterFcmToken([FromBody] UnregisterFcmTokenDto dto)
        {
            var result = await _pushNotificationService.UnregisterFcmTokenAsync(CurrentUserId, dto.FcmToken);
            return result ? Ok(new { success = true, message = "FCM token unregistered successfully" })
                         : BadRequest(new { success = false, message = "Failed to unregister FCM token" });
        }
    }

    public record SendPhoneChangeOtpDto(string NewPhone);
    public record UpdatePhoneDto(string NewPhone, string OtpCode);

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
