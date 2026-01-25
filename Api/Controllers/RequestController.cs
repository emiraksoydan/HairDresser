using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class RequestController : BaseApiController
    {
        private readonly IRequestService _requestService;

        public RequestController(IRequestService requestService)
        {
            _requestService = requestService;
        }

        /// <summary>
        /// Yeni istek oluştur - gumusmakastr@gmail.com adresine email gönderilir
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateRequestDto dto)
        {
            return await HandleUserDataOperation(userId => _requestService.CreateRequestAsync(userId, dto));
        }

        /// <summary>
        /// Kullanıcının isteklerini getir
        /// </summary>
        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            return await HandleUserDataOperation(userId => _requestService.GetMyRequestsAsync(userId));
        }

        /// <summary>
        /// İsteği sil
        /// </summary>
        [HttpDelete("{requestId}")]
        public async Task<IActionResult> Delete(Guid requestId)
        {
            return await HandleUserDataOperation(userId => _requestService.DeleteRequestAsync(userId, requestId));
        }
    }
}
