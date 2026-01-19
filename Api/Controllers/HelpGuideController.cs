using Business.Abstract;
using Business.Resources;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class HelpGuideController : BaseApiController
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

            return await HandleDataResultAsync(_helpGuideService.GetActiveByUserTypeAsync(userType));
        }
    }
}
