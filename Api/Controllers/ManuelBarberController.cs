using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ManuelBarberController : BaseApiController
    {
        private readonly IManuelBarberService _manuelBarberService;

        public ManuelBarberController(IManuelBarberService manuelBarberService)
        {
            _manuelBarberService = manuelBarberService;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ManuelBarberCreateDto dto)
        {
            return await HandleResultAsync(_manuelBarberService.AddAsync(dto));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ManuelBarberUpdateDto dto)
        {
            return await HandleResultAsync(_manuelBarberService.UpdateAsync(dto));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleResultAsync(_manuelBarberService.DeleteAsync(id));
        }
    }
}
