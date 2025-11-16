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
        public async Task<IActionResult> CreateForFreeBarber()
        {

            

            return  Ok();
        }

        [HttpPost("store")]
        public async Task<IActionResult> CreateForStore()
        {
            
            return  Ok();
        }
  
    }
   
}
