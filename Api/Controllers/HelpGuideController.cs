using Business.Abstract;
using Business.Resources;
using Core.Extensions;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class HelpGuideController : ControllerBase
    {
        private readonly IHelpGuideService _helpGuideService;

        public HelpGuideController(IHelpGuideService helpGuideService)
        {
            _helpGuideService = helpGuideService;
        }

        [HttpGet("{userType}")]
        public async Task<IActionResult> GetByUserType(int userType)
        {
            // UserType enum değerini kontrol et
            if (!Enum.IsDefined(typeof(UserType), userType))
            {
                return BadRequest(new { success = false, message = Messages.InvalidUserType });
            }

            var result = await _helpGuideService.GetActiveByUserTypeAsync(userType);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
