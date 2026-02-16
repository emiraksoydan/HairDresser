using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class FreeBarberController : BaseApiController
    {
        private readonly IFreeBarberService _freeBarberService;

        public FreeBarberController(IFreeBarberService freeBarberService)
        {
            _freeBarberService = freeBarberService;
        }

        [HttpPost("create-free-barber")]
        public async Task<IActionResult> Add([FromBody] FreeBarberCreateDto dto)
        {
            var result = await _freeBarberService.Add(dto, CurrentUserId);
            return HandleDataResult(result);
        }

        [HttpPut("update-free-barber")]
        public async Task<IActionResult> Update([FromBody] FreeBarberUpdateDto dto)
        {
            return await HandleUpdateOperation(dto, _freeBarberService.Update);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleResultAsync(_freeBarberService.DeleteAsync(id));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            return await HandleDataResultAsync(_freeBarberService.GetMyPanelDetail(id));
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lon, [FromQuery] double distance = 10.0)
        {
            var currentUserId = User.GetUserIdOrNull(); // Optional: giriş yapmamış kullanıcılar da görebilmeli
            return await HandleDataResultAsync(_freeBarberService.GetNearbyFreeBarberAsync(lat, lon, distance, currentUserId));
        }

        [HttpPost("filtered")]
        public async Task<IActionResult> GetFiltered([FromBody] FilterRequestDto filter)
        {
            filter.CurrentUserId = CurrentUserId; // Set current user for favorites
            return await HandleDataResultAsync(_freeBarberService.GetFilteredFreeBarbersAsync(filter));
        }

        [HttpGet("mypanel")]
        public async Task<IActionResult> GetMine()
        {
            return await HandleUserDataOperation(userId => _freeBarberService.GetMyPanel(userId));
        }

        [HttpGet("get-freebarber-for-users")]
        public async Task<IActionResult> GetFreeBarberForUsers([FromQuery] Guid freeBarberId)
        {
            return await HandleDataResultAsync(_freeBarberService.GetFreeBarberForUsers(freeBarberId));
        }

        [EnableRateLimiting("location")]
        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationDto req)
        {
            return await HandleUserOperation(userId => _freeBarberService.UpdateLocationAsync(req, userId));
        }

        [HttpPost("update-availability")]
        public async Task<IActionResult> UpdateAvailability([FromQuery] bool isAvailable)
        {
            return await HandleUserOperation(userId => _freeBarberService.UpdateAvailabilityAsync(isAvailable, userId));
        }
    }
}
