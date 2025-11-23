using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceOfferingController : ControllerBase
    {
        private readonly IServiceOfferingService _serviceOfferingService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ServiceOfferingController(IServiceOfferingService serviceOfferingService, IHttpContextAccessor httpContextAccessor)
        {
            _serviceOfferingService = serviceOfferingService;
            _httpContextAccessor = httpContextAccessor;
        }
        private Guid CurrentUserId =>
            Guid.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("identifier")?.Value!);

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ServiceOfferingCreateDto dto)
        {
            var result = await _serviceOfferingService.Add(dto, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ServiceOfferingUpdateDto dto)
        {
            var result = await _serviceOfferingService.Update(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _serviceOfferingService.DeleteAsync(id, CurrentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _serviceOfferingService.GetByIdAsync(id);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }
        [HttpGet("getalloffering/{byId}")]
        public async Task<IActionResult> GetAllOfferingByStoreId(Guid byId)
        {
            var result = await _serviceOfferingService.GetServiceOfferingsIdAsync(byId);
            return result.Success ? Ok(result.Data) : NotFound(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _serviceOfferingService.GetAll();
            return result.Success ? Ok(result.Data) : BadRequest(result);
        }
    }
}
