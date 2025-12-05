using Business.Abstract;
using Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("{appointmentId:guid}/message")]
        public async Task<IActionResult> Send(Guid appointmentId, [FromBody] SendMessageRequest req)
        {
            var result = await _chatService.SendMessageAsync(User.GetUserIdOrThrow(), appointmentId, req.Text);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{appointmentId:guid}/read")]
        public async Task<IActionResult> Read(Guid appointmentId)
        {
            var result = await _chatService.MarkThreadReadAsync(User.GetUserIdOrThrow(), appointmentId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("threads")]
        public async Task<IActionResult> Threads()
        {
            var result = await _chatService.GetThreadsAsync(User.GetUserIdOrThrow());
            return Ok(result);
        }

        [HttpGet("{appointmentId:guid}/messages")]
        public async Task<IActionResult> Messages(Guid appointmentId, [FromQuery] DateTime? before)
        {
            // before: UTC gönder (RN’de new Date().toISOString())
            var result = await _chatService.GetMessagesAsync(User.GetUserIdOrThrow(), appointmentId, before);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
    public class SendMessageRequest
    {
        [Required]
        [MinLength(1)]
        public string Text { get; set; } = "";
    }
}
