using System.ComponentModel.DataAnnotations;
using Business.Abstract;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

        private Guid GetUserId()
        {
            var idStr = User.FindFirst("identifier")?.Value;
            if (string.IsNullOrWhiteSpace(idStr)) throw new UnauthorizedAccessException("identifier claim missing");
            return Guid.Parse(idStr);
        }

        [HttpPost("freebarber")]
        public async Task<IActionResult> CreateForFreeBarber([FromBody] CreateForFreeBarberRequest req)
        {

            var customerId = GetUserId();
            var result = await _svc.CustomerCreatesForFreeBarberAsync(
                customerId,
                req.FreeBarberUserId,
                req.StartUtc,
                req.EndUtc, req.ServiceOfferingIds);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("store")]
        public async Task<IActionResult> CreateForStore([FromBody] CreateForStoreRequest req)
        {
            var customerId = GetUserId();
            var performerUserId = req.PerformerUserId ?? Guid.Empty;
            var result = await _svc.CustomerCreatesForStoreAsync(
                customerId,
                req.ChairId,
                performerUserId,
                req.StartUtc,
                req.EndUtc, req.ServiceOfferingIds);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{appointmentId:guid}/approve")]
        public async Task<IActionResult> Approve([FromRoute] Guid appointmentId, [FromBody] ApproveRequest req)
        {
            var approveByUserId = GetUserId();
            var result = await _svc.ApproveAsync(appointmentId, approveByUserId, req.Approve);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("store-invite-barber")]
        public async Task<IActionResult> StoreInvitesBarber([FromBody] StoreInvitesBarberRequest req)
        {

            var storeOwnerUserId = GetUserId();
            var result = await _svc.StoreInvitesBarberAsync(
                storeOwnerUserId,
                req.FreeBarberUserId,
                req.StoreId,
                req.StartUtc,
                req.EndUtc);

            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
    public class CreateForFreeBarberRequest
    {
        public Guid FreeBarberUserId { get; set; }

        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public List<Guid> ServiceOfferingIds { get; set; } = new();

    }

    public class CreateForStoreRequest
    {
        public Guid? PerformerUserId { get; set; }

        public Guid ChairId { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }

        public List<Guid> ServiceOfferingIds { get; set; } = new();
    }

    public class SelectStoreRequest
    {
        public Guid StoreId { get; set; }
        public Guid? ChairId { get; set; } // akışına göre zorunlu yapabilirsin
    }

    public class ApproveRequest
    {
        public bool Approve { get; set; }
    }

    public class StoreInvitesBarberRequest
    {
        public Guid FreeBarberUserId { get; set; }
        public Guid StoreId { get; set; }

        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }
}
