using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class RatingController : BaseApiController
    {
        private readonly IRatingService _ratingService;

        public RatingController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateRatingDto dto)
        {
            return await HandleUserDataOperation(userId => _ratingService.CreateRatingAsync(userId, dto));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleUserDataOperation(userId => _ratingService.DeleteRatingAsync(userId, id));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            return await HandleDataResultAsync(_ratingService.GetRatingByIdAsync(id));
        }

        [HttpGet("target/{targetId}")]
        public async Task<IActionResult> GetByTarget(Guid targetId)
        {
            return await HandleDataResultAsync(_ratingService.GetRatingsByTargetAsync(targetId));
        }

        [HttpGet("appointment/{appointmentId}/target/{targetId}")]
        public async Task<IActionResult> GetMyRatingForAppointment(Guid appointmentId, Guid targetId)
        {
            return await HandleUserDataOperation(userId => _ratingService.GetMyRatingForAppointmentAsync(userId, appointmentId, targetId));
        }
    }
}
