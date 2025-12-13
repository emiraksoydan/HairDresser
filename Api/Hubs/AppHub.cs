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
    }
}
