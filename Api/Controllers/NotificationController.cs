using Business.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class NotificationController : BaseApiController
    {
        private readonly INotificationService _svc;

        public NotificationController(INotificationService svc)
        {
            _svc = svc;
        }

        [HttpPost("read/{id:guid}")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            return await HandleUserDataOperation(userId => _svc.MarkReadAsync(userId, id));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return await HandleUserDataOperation(userId => _svc.GetAllNotify(userId));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleUserDataOperation(userId => _svc.DeleteAsync(userId, id));
        }

        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAll()
        {
            return await HandleUserDataOperation(userId => _svc.DeleteAllAsync(userId));
        }
    }
}
