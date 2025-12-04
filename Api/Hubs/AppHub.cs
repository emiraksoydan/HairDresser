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
            // istersen remove de yapabilirsin (şart değil)
            await base.OnDisconnectedAsync(exception);
        }
    }
}
