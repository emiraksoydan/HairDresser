using Business.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkingController(IWorkingHourService workingHourService) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] WorkingHourCreateDto dto)
        {
            var result = await workingHourService.AddAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] WorkingHourUpdateDto dto)
        {
            var result = await workingHourService.UpdateAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await workingHourService.DeleteAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{targetId}")]
        public async Task<IActionResult> Get(Guid targetId)
        {
            var result = await workingHourService.GetByTargetAsync(targetId);
            return result.Success ? Ok(result) : NotFound(result);
        }
    }
}
