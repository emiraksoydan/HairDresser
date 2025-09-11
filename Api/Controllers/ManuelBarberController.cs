using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ManuelBarberController(IManuelBarberService manuelBarberService, IHttpContextAccessor accessor) : ControllerBase
    {
        private Guid CurrentUserId =>
      Guid.Parse(accessor.HttpContext?.User.FindFirst("identifier")?.Value!);

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ManuelBarberCreateDto dto)
        {
            var result = await manuelBarberService.AddAsync(dto, CurrentUserId);
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

        [HttpGet]
        public async Task<IActionResult> GetMyBarbers()
        {
            var result = await manuelBarberService.GetAllByStoreAsync(CurrentUserId);
            return result.Success ? Ok(result) : NotFound(result);
        }
    }
}
