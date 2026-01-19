using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class SettingController : BaseApiController
    {
        private readonly ISettingService _settingService;

        public SettingController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return await HandleUserDataOperation(userId => _settingService.GetByUserIdAsync(userId));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] SettingUpdateDto dto)
        {
            return await HandleUserOperation(userId => _settingService.UpdateAsync(userId, dto));
        }
    }
}
