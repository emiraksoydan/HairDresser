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


        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] Guid storeId, [FromQuery] DateOnly dateOnly, CancellationToken ct)
        {
            var data = await _svc.GetAvailibity(storeId, dateOnly, ct);
            return Ok(data);
        }

    }
   
}
