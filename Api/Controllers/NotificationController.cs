using Business.Abstract;
using DataAccess.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _svc;
        private Guid GetUserId()
        {
            var idStr = User.FindFirst("identifier")?.Value;
            if (string.IsNullOrWhiteSpace(idStr)) throw new UnauthorizedAccessException("identifier claim missing");
            return Guid.Parse(idStr);
        }

        public NotificationController( INotificationService svc) {  _svc = svc; }



        [HttpGet("unread-count")]
        public async Task<IActionResult> Unread()
        {
            var result = await _svc.GetUnreadCountAsync(GetUserId());
            return result.Success ? Ok(result) : NotFound(result);

        }
        [HttpGet("getall")]
        public async Task<IActionResult> GetAllNotifyList()
        {
            var result = await _svc.GetAllNotify(GetUserId());
            return result.Success ? Ok(result) : NotFound(result);

        }
    }
}
