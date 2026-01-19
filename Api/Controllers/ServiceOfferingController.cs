using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ServiceOfferingController : BaseApiController
    {
        private readonly IServiceOfferingService _serviceOfferingService;

        public ServiceOfferingController(IServiceOfferingService serviceOfferingService)
        {
            _serviceOfferingService = serviceOfferingService;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ServiceOfferingCreateDto dto)
        {
            return await HandleCreateOperation(dto, _serviceOfferingService.Add);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ServiceOfferingUpdateDto dto)
        {
            return await HandleResultAsync(_serviceOfferingService.Update(dto));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleDeleteOperation(id, _serviceOfferingService.DeleteAsync);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            return await HandleDataResultAsync(_serviceOfferingService.GetByIdAsync(id));
        }

        [HttpGet("getalloffering/{byId}")]
        public async Task<IActionResult> GetAllOfferingByStoreId(Guid byId)
        {
            return await HandleDataResultAsync(_serviceOfferingService.GetServiceOfferingsIdAsync(byId));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return await HandleDataResultAsync(_serviceOfferingService.GetAll());
        }
    }
}
