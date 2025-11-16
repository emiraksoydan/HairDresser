using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BarberStoreController : ControllerBase
    {
        private readonly IBarberStoreService _storeService;
        public BarberStoreController(IBarberStoreService storeService)
        {
            _storeService = storeService;
        }
        private Guid CurrentUserId => User.GetUserIdOrThrow();

        [HttpPost("create-store")]
        public async Task<IActionResult> Add([FromBody] BarberStoreCreateDto dto)
        {
            var result = await _storeService.Add(dto, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BarberStoreUpdateDto dto)
        {
            var result = await _storeService.Update(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _storeService.DeleteAsync(id, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _storeService.GetByIdAsync(id);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }
        [HttpGet("operation/{storeid}")]
        public async Task<IActionResult> GetStoreOperation(Guid storeid)
        {
            var result = await _storeService.GetByIdStoreOperation(storeid);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lon, [FromQuery] double distance = 1.0)
        {
            var result = await _storeService.GetNearbyStoresAsync(lat, lon, distance);
            return result.Success ? Ok(result.Data) : BadRequest(result);
        }
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            var result = await _storeService.GetByCurrentUserAsync(CurrentUserId);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }
    }
}
