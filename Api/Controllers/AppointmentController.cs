using Business.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class AppointmentController : BaseApiController
    {
        private readonly IAppointmentService _svc;

        public AppointmentController(IAppointmentService svc)
        {
            _svc = svc;
        }

        [HttpGet("getallbyfilter")]
        public async Task<IActionResult> GetAllByFilter([FromQuery] AppointmentFilter filter)
        {
            return await HandleUserDataOperation(userId => _svc.GetAllAppointmentByFilter(userId, filter));
        }

        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] Guid storeId, [FromQuery] DateOnly dateOnly, CancellationToken ct)
        {
            return await HandleDataResultAsync(_svc.GetAvailibity(storeId, dateOnly, ct));
        }

        [HttpPost("customer-to-freebarber")]
        public async Task<IActionResult> CreateCustomerToFreeBarber([FromBody] CreateAppointmentRequestDto req)
        {
            return await HandleUserDataOperation(userId => _svc.CreateCustomerToFreeBarberAsync(userId, req));
        }

        [HttpPost("customer")]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateAppointmentRequestDto req)
        {
            return await HandleUserDataOperation(userId => _svc.CreateCustomerToStoreControlAsync(userId, req));
        }

        [HttpPost("{id:guid}/add-store")]
        public async Task<IActionResult> AddStoreToAppointment(
            Guid id,
            [FromBody] AddStoreToAppointmentRequestDto req)
        {
            return await HandleUserDataOperation(userId =>
                _svc.AddStoreToExistingAppointmentAsync(
                    userId, id, req.StoreId, req.ChairId, req.AppointmentDate, req.StartTime, req.EndTime, req.ServiceOfferingIds));
        }

        [HttpPost("freebarber")]
        public async Task<IActionResult> CreateFreeBarber([FromBody] CreateAppointmentRequestDto req)
        {
            return await HandleUserDataOperation(userId => _svc.CreateFreeBarberToStoreAsync(userId, req));
        }

        [HttpPost("store")]
        public async Task<IActionResult> CreateStoreToFreeBarber([FromBody] CreateStoreToFreeBarberRequestDto req)
        {
            return await HandleUserDataOperation(userId => _svc.CreateStoreToFreeBarberAsync(userId, req));
        }

        [HttpPost("store/call-freebarber")]
        public async Task<IActionResult> CallFreeBarber([FromBody] CreateStoreToFreeBarberRequestDto req)
        {
            return await HandleUserDataOperation(userId => _svc.CreateStoreToFreeBarberAsync(userId, req));
        }

        [HttpPost("{id:guid}/store-decision")]
        public async Task<IActionResult> StoreDecision(Guid id, [FromQuery] bool approve)
        {
            return await HandleUserDataOperation(userId => _svc.StoreDecisionAsync(userId, id, approve));
        }

        [HttpPost("{id:guid}/freebarber-decision")]
        public async Task<IActionResult> FreeBarberDecision(Guid id, [FromQuery] bool approve)
        {
            return await HandleUserDataOperation(userId => _svc.FreeBarberDecisionAsync(userId, id, approve));
        }

        [HttpPost("{id:guid}/customer-decision")]
        public async Task<IActionResult> CustomerDecision(Guid id, [FromQuery] bool approve)
        {
            return await HandleUserDataOperation(userId => _svc.CustomerDecisionAsync(userId, id, approve));
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            return await HandleUserDataOperation(userId => _svc.CancelAsync(userId, id));
        }

        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            return await HandleUserDataOperation(userId => _svc.CompleteAsync(userId, id));
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
