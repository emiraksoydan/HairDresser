using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FreeBarberController : ControllerBase
    {
        private readonly IFreeBarberService _freeBarberService;
        public FreeBarberController(IFreeBarberService freeBarberService)
        {
            _freeBarberService = freeBarberService;
        }
        private Guid CurrentUserId => User.GetUserIdOrThrow();

        [HttpPost("create-free-barber")]
        public async Task<IActionResult> Add([FromBody] FreeBarberCreateDto dto)
        {
            var result = await _freeBarberService.Add(dto, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("update-free-barber")]
        public async Task<IActionResult> Update([FromBody] FreeBarberUpdateDto dto)
        {
            var result = await _freeBarberService.Update(dto, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _freeBarberService.DeleteAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _freeBarberService.GetMyPanelDetail(id);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lon, [FromQuery] double distance = 1.0)
        {
            var result = await _freeBarberService.GetNearbyFreeBarberAsync(lat, lon, distance);
            return result.Success ? Ok(result.Data) : BadRequest(result);
        }
        [HttpGet("mypanel")]
        public async Task<IActionResult> GetMine()
        {
            var result = await _freeBarberService.GetMyPanel(CurrentUserId);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }

        [HttpGet("get-freebarber-for-users")]
        public async Task<IActionResult> GetFreeBarberForUsers([FromQuery] Guid freeBarberId)
        {
            var result = await _freeBarberService.GetFreeBarberForUsers(freeBarberId);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }
    }
}
