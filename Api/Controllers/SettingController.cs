using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingController : ControllerBase
    {
        private readonly ISettingService _settingService;

        public SettingController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _settingService.GetByUserIdAsync(userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] SettingUpdateDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _settingService.UpdateAsync(userId, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}

