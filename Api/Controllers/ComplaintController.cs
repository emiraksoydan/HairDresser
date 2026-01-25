using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ComplaintController : BaseApiController
    {
        private readonly IComplaintService _complaintService;

        public ComplaintController(IComplaintService complaintService)
        {
            _complaintService = complaintService;
        }

        /// <summary>
        /// Yeni şikayet oluştur
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateComplaintDto dto)
        {
            return await HandleUserDataOperation(userId => _complaintService.CreateComplaintAsync(userId, dto));
        }

        /// <summary>
        /// Kullanıcının şikayetlerini getir
        /// </summary>
        [HttpGet("my-complaints")]
        public async Task<IActionResult> GetMyComplaints()
        {
            return await HandleUserDataOperation(userId => _complaintService.GetMyComplaintsAsync(userId));
        }

        /// <summary>
        /// Şikayeti sil
        /// </summary>
        [HttpDelete("{complaintId}")]
        public async Task<IActionResult> Delete(Guid complaintId)
        {
            return await HandleUserDataOperation(userId => _complaintService.DeleteComplaintAsync(userId, complaintId));
        }
    }
}
