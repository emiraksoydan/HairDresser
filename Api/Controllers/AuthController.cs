using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login(UserForLoginDto dto)
        {
            var res = await authService.Login(dto);
            return res.Success
                ? Ok(res.Data)
                : BadRequest(new { message = res.Message });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserForRegisterDto dto)
        {
            var res = await authService.Register(dto, dto.Password);
            return res.Success
                ? Ok(res.Data)
                : BadRequest(new { message = res.Message });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> Refresh(string refreshToken)
        {
            var res = await authService.Refresh(refreshToken);
            return res.Success
                ? Ok(res.Data)
                : BadRequest(new { message = res.Message });
        }
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(string refreshToken)
        {
            var result = await authService.Logout(refreshToken);
            return result.Success
                ? Ok(new { message = result.Message }) // ✅
                : BadRequest(new { message = result.Message });
        }
    }
}
