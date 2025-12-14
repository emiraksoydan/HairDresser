using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RatingController : ControllerBase
    {
        private readonly IRatingService _ratingService;

        public RatingController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateRatingDto dto)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _ratingService.CreateRatingAsync(userId, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _ratingService.DeleteRatingAsync(userId, id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _ratingService.GetRatingByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpGet("target/{targetId}")]
        public async Task<IActionResult> GetByTarget(Guid targetId)
        {
            var result = await _ratingService.GetRatingsByTargetAsync(targetId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("appointment/{appointmentId}/target/{targetId}")]
        public async Task<IActionResult> GetMyRatingForAppointment(Guid appointmentId, Guid targetId)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _ratingService.GetMyRatingForAppointmentAsync(userId, appointmentId, targetId);
            return result.Success ? Ok(result) : NotFound(result);
        }
    }
}
