using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class BarberStoreChairController : BaseApiController
    {
        private readonly IBarberStoreChairService _barberStoreChairService;

        public BarberStoreChairController(IBarberStoreChairService barberStoreChairService)
        {
            _barberStoreChairService = barberStoreChairService;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] BarberChairCreateDto dto)
        {
            return await HandleResultAsync(_barberStoreChairService.AddAsync(dto));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BarberChairUpdateDto dto)
        {
            return await HandleResultAsync(_barberStoreChairService.UpdateAsync(dto));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleResultAsync(_barberStoreChairService.DeleteAsync(id));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            return await HandleDataResultAsync(_barberStoreChairService.GetAllByStoreAsync(id));
        }
    }
}
