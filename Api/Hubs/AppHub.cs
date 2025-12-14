using Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace Api.Hubs
{
    [Authorize]
    public class AppHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userIdStr = Context?.User?.GetUserIdOrThrow();

            if (Guid.TryParse(userIdStr.ToString(), out var userId))
                await Groups.AddToGroupAsync(Context?.ConnectionId!, $"user:{userId}");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Group'tan çıkar - memory leak'i önlemek için
            var userIdStr = Context?.User?.GetUserIdOrThrow();
            if (Guid.TryParse(userIdStr?.ToString(), out var userId))
            {
                await Groups.RemoveFromGroupAsync(Context?.ConnectionId!, $"user:{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Typing indicator için hub metodu (frontend'den çağrılacak)
        public async Task NotifyTyping(string threadId, bool isTyping)
        {
            var userIdStr = Context?.User?.GetUserIdOrThrow();
            if (!Guid.TryParse(userIdStr?.ToString(), out var userId) || !Guid.TryParse(threadId, out var threadIdGuid))
                return;

            // Bu metod için ChatService üzerinden typing event'i göndermek daha mantıklı
            // Ama hub üzerinden direkt de yapılabilir - frontend'den çağrılacak
            // Backend'den ChatService.NotifyTypingAsync metodu çağrılacak (API Controller üzerinden)
        }
    }
}
