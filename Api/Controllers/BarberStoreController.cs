using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class BarberStoreController : BaseApiController
    {
        private readonly IBarberStoreService _storeService;

        public BarberStoreController(IBarberStoreService storeService)
        {
            _storeService = storeService;
        }

        [HttpPost("create-store")]
        public async Task<IActionResult> Add([FromBody] BarberStoreCreateDto dto)
        {
            var result = await _storeService.Add(dto, CurrentUserId);
            return HandleDataResult(result);
        }

        [HttpPut("update-store")]
        public async Task<IActionResult> Update([FromBody] BarberStoreUpdateDto dto)
        {
            return await HandleUpdateOperation(dto, _storeService.Update);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleDeleteOperation(id, _storeService.DeleteAsync);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            return await HandleDataResultAsync(_storeService.GetByIdAsync(id));
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lon, [FromQuery] double distance = 10.0)
        {
            var currentUserId = User.GetUserIdOrNull(); // Optional: giriş yapmamış kullanıcılar da görebilmeli
            return await HandleDataResultAsync(_storeService.GetNearbyStoresAsync(lat, lon, distance, currentUserId));
        }

        [HttpPost("filtered")]
        public async Task<IActionResult> GetFiltered([FromBody] FilterRequestDto filter)
        {
            filter.CurrentUserId = CurrentUserId; // Set current user for favorites
            return await HandleDataResultAsync(_storeService.GetFilteredStoresAsync(filter));
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            return await HandleUserDataOperation(userId => _storeService.GetByCurrentUserAsync(userId));
        }

        [HttpGet("get-store-for-users")]
        public async Task<IActionResult> GetStoreForUsers([FromQuery] Guid storeId)
        {
            return await HandleDataResultAsync(_storeService.GetBarberStoreForUsers(storeId));
        }
    }
}
