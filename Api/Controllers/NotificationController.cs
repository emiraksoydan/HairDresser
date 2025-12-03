using Business.Abstract;
using Core.Extensions;
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
 
        public NotificationController( INotificationService svc) {  _svc = svc; }

        [HttpPost("read/{id:guid}")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var result = await _svc.MarkReadAsync(User.GetUserIdOrThrow(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.GetAllNotify(userId);
            return Ok(result);
        }

    }
}
