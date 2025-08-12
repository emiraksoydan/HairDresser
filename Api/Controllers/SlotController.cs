using Business.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SlotController : ControllerBase
    {
        private readonly ISlotService _slotService;

        public SlotController(ISlotService slotService)
        {
            _slotService = slotService;
        }
        [HttpGet("weekly")]
        public async Task<IActionResult> GetWeeklySlots([FromQuery] Guid storeId)
        {
            var result = await _slotService.GetWeeklySlotsAsync(storeId);
            if(result.Success)
                return Ok(result.Data);
            return BadRequest(new { message = result.Message });

        }
    }
}
