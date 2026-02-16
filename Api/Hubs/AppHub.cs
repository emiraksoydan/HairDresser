using Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Api.Hubs
{
    [Authorize]
    public class AppHub : Hub
    {
        private readonly ILogger<AppHub> _logger;

        public AppHub(ILogger<AppHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                if (Context?.User != null && !string.IsNullOrEmpty(Context.ConnectionId))
                {
                    var userId = Context.User.GetUserIdOrThrow();
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
                    _logger.LogInformation("[SignalR] User {UserId} connected and added to group user:{UserId} with ConnectionId: {ConnectionId}",
                        userId, userId, Context.ConnectionId);
                }

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignalR] Error in OnConnectedAsync");
                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                if (Context?.User != null && !string.IsNullOrEmpty(Context.ConnectionId))
                {
                    var userId = Context.User.GetUserIdOrThrow();
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
                    _logger.LogInformation("[SignalR] User {UserId} disconnected and removed from group with ConnectionId: {ConnectionId}",
                        userId, Context.ConnectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignalR] Error in OnDisconnectedAsync");
            }
            finally
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        /// <summary>
        /// Frontend'den bağlantı kurulduktan sonra çağrılır - gruba katılmayı garanti eder
        /// </summary>
        public async Task JoinUserGroup()
        {
            try
            {
                if (Context?.User != null && !string.IsNullOrEmpty(Context.ConnectionId))
                {
                    var userId = Context.User.GetUserIdOrThrow();
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
                    _logger.LogInformation("[SignalR] JoinUserGroup called - User {UserId} added to group with ConnectionId: {ConnectionId}",
                        userId, Context.ConnectionId);

                    // Başarılı katılımı frontend'e bildir
                    await Clients.Caller.SendAsync("group.joined", new { userId, success = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignalR] Error in JoinUserGroup");
                await Clients.Caller.SendAsync("group.joined", new { success = false, error = ex.Message });
            }
        }

    }
}
