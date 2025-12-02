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
            var userId = Context.User!.GetUserIdOrThrow();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            await base.OnConnectedAsync();
        }
    }
}
