using Business.Abstract;
using Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.Intrinsics.Arm;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BadgeController : ControllerBase
    {
        private readonly IBadgeService _svc;

        public BadgeController(IBadgeService svc)
        {
            _svc = svc;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await _svc.GetCountsAsync(User.GetUserIdOrThrow());
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
