using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] UserForSendOtpDto req)
        {
            var r = await authService.SendOtpAsync(req.PhoneNumber,req.UserType,req.OtpPurpose);
            return r.Success ? Ok(r) : BadRequest(r);
        }
        [AllowAnonymous]
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] UserForVerifyDto req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var device = req.Device ?? Request.Headers.UserAgent.ToString();
            var res = await authService.VerifyOtpAsync(req, ip, device);
            return res.Success ? Ok(res) : BadRequest(res.Message);
        }
        [AllowAnonymous]
        [HttpPost("password")]
        public async Task<IActionResult> SendOtp([FromBody] UserForVerifyDto req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var device = req.Device ?? Request.Headers.UserAgent.ToString();
            var res = await authService.LoginWithPassword(req, ip, device);
            return res.Success ? Ok(res) : BadRequest(res.Message);
        }
        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var res = await authService.RefreshAsync(req.RefreshToken, ip);
            return res.Success ? Ok(res) : Unauthorized(res.Message);
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] RefreshTokenDto req)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var r = await authService.RevokeAsync(userId, req.RefreshToken, ip);
            return r.Success ? Ok(r) : BadRequest(r);
        }
    }
}
