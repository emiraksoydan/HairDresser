using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class WorkingController : BaseApiController
    {
        private readonly IWorkingHourService _workingHourService;

        public WorkingController(IWorkingHourService workingHourService)
        {
            _workingHourService = workingHourService;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] WorkingHourCreateDto dto)
        {
            return await HandleResultAsync(_workingHourService.AddAsync(dto));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] WorkingHourUpdateDto dto)
        {
            return await HandleResultAsync(_workingHourService.UpdateAsync(dto));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleResultAsync(_workingHourService.DeleteAsync(id));
        }

        [HttpGet("{targetId}")]
        public async Task<IActionResult> Get(Guid targetId)
        {
            return await HandleDataResultAsync(_workingHourService.GetByTargetAsync(targetId));
        }
    }
}
