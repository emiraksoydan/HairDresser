using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ManuelBarberController(IManuelBarberService manuelBarberService) : ControllerBase
    {

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ManuelBarberCreateDto dto)
        {
            var result = await manuelBarberService.AddAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ManuelBarberUpdateDto dto)
        {
            var result = await manuelBarberService.UpdateAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await manuelBarberService.DeleteAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

    }
}
