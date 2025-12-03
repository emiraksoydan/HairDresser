using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentService _svc;

        public AppointmentController(IAppointmentService svc)
        {
            _svc = svc;
        }


        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] Guid storeId, [FromQuery] DateOnly dateOnly, CancellationToken ct)
        {
            var data = await _svc.GetAvailibity(storeId, dateOnly, ct);
            return Ok(data);
        }

        [HttpPost("customer")]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateAppointmentRequestDto req)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.CreateCustomerToStoreAndFreeBarberControlAsync(userId, req);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // 3) FreeBarber -> Store
        [HttpPost("freebarber")]
        public async Task<IActionResult> CreateFreeBarber([FromBody] CreateAppointmentRequestDto req)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.CreateFreeBarberToStoreAsync(userId, req);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // 4) Store -> FreeBarber call
        [HttpPost("store")]
        public async Task<IActionResult> CreateStoreToFreeBarber([FromBody] CreateAppointmentRequestDto req)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.CreateStoreToFreeBarberAsync(userId, req);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/store-decision")]
        public async Task<IActionResult> StoreDecision(Guid id, [FromQuery] bool approve)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.StoreDecisionAsync(userId, id, approve);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/freebarber-decision")]
        public async Task<IActionResult> FreeBarberDecision(Guid id, [FromQuery] bool approve)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.FreeBarberDecisionAsync(userId, id, approve);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.CancelAsync(userId, id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _svc.CompleteAsync(userId, id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

    }
   
}
