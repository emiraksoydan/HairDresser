using Business.Abstract;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ChatController : BaseApiController
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("{appointmentId:guid}/message")]
        public async Task<IActionResult> Send(Guid appointmentId, [FromBody] SendMessageRequest req)
        {
            return await HandleUserDataOperation(userId => _chatService.SendMessageAsync(userId, appointmentId, req.Text));
        }

        [HttpPost("{appointmentId:guid}/read")]
        public async Task<IActionResult> Read(Guid appointmentId)
        {
            // Geriye dönük uyumluluk için AppointmentId ile okundu işaretleme
            return await HandleUserDataOperation(userId => _chatService.MarkThreadReadByAppointmentAsync(userId, appointmentId));
        }

        [HttpPost("thread/{threadId:guid}/message")]
        public async Task<IActionResult> SendToThread(Guid threadId, [FromBody] SendMessageRequest req)
        {
            return await HandleUserDataOperation(userId => _chatService.SendFavoriteMessageAsync(userId, threadId, req.Text));
        }

        [HttpPost("thread/{threadId:guid}/read")]
        public async Task<IActionResult> ReadThread(Guid threadId)
        {
            return await HandleUserDataOperation(userId => _chatService.MarkThreadReadAsync(userId, threadId));
        }

        [HttpGet("threads")]
        public async Task<IActionResult> Threads()
        {
            var result = await _chatService.GetThreadsAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpGet("{appointmentId:guid}/messages")]
        public async Task<IActionResult> Messages(Guid appointmentId, [FromQuery] DateTime? before)
        {
            // before: UTC gönder (RN'de new Date().toISOString())
            return await HandleUserDataOperation(userId => _chatService.GetMessagesAsync(userId, appointmentId, before));
        }

        [HttpGet("thread/{threadId:guid}/messages")]
        public async Task<IActionResult> ThreadMessages(Guid threadId, [FromQuery] DateTime? before)
        {
            // ThreadId ile mesaj getirme (hem randevu hem favori thread'leri için)
            return await HandleUserDataOperation(userId => _chatService.GetMessagesByThreadAsync(userId, threadId, before));
        }

        [HttpPost("thread/{threadId:guid}/typing")]
        public async Task<IActionResult> NotifyTyping(Guid threadId, [FromBody] TypingRequest req)
        {
            return await HandleUserDataOperation(userId => _chatService.NotifyTypingAsync(userId, threadId, req.IsTyping));
        }
    }

    public class SendMessageRequest
    {
        [Required]
        [MinLength(1)]
        public string Text { get; set; } = "";
    }

    public class TypingRequest
    {
        [Required]
        public bool IsTyping { get; set; }
    }
}
