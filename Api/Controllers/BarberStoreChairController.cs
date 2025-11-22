using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BarberStoreChairController : ControllerBase
    {
        private readonly IBarberStoreChairService _barberStoreChairService;
        public BarberStoreChairController(IBarberStoreChairService barberStoreChairService)
        {
            _barberStoreChairService = barberStoreChairService;
        }
     

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] BarberChairCreateDto dto)
        {
            var result = await _barberStoreChairService.AddAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BarberChairUpdateDto dto)
        {
            var result = await _barberStoreChairService.UpdateAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _barberStoreChairService.DeleteAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _barberStoreChairService.GetAllByStoreAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

  
    }
}
