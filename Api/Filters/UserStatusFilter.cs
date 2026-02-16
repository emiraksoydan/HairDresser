using Business.Resources;
using DataAccess.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Filters
{
    public class UserStatusFilter(IUserDal userDal, IMemoryCache cache) : IAsyncActionFilter
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // AllowAnonymous endpoint'leri atla
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await next();
                return;
            }

            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            var userIdStr = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                await next();
                return;
            }

            // DB sorgusunu kısa süreli cache ile sarmalıyoruz (her istek DB'yi çarpmıyor)
            var cacheKey = $"user_status_{userId}";
            if (!cache.TryGetValue(cacheKey, out UserStatusCache? status))
            {
                var dbUser = await userDal.Get(u => u.Id == userId);
                status = dbUser == null ? null : new UserStatusCache
                {
                    IsBanned = dbUser.IsBanned,
                    BanReason = dbUser.BanReason,
                    UserType = dbUser.UserType.ToString(),
                    TrialEndDate = dbUser.TrialEndDate,
                    SubscriptionEndDate = dbUser.SubscriptionEndDate
                };
                cache.Set(cacheKey, status, CacheDuration);
            }

            if (status == null)
            {
                await next();
                return;
            }

            // Ban kontrolü
            if (status.IsBanned)
            {
                var message = string.IsNullOrWhiteSpace(status.BanReason)
                    ? Messages.UserBanned
                    : string.Format(Messages.UserBannedWithReason, status.BanReason);

                context.Result = new ObjectResult(new { success = false, message }) { StatusCode = 403 };
                return;
            }

            // Abonelik kontrolü - sadece FreeBarber ve BarberStore için
            if (status.UserType is "FreeBarber" or "BarberStore")
            {
                var trialActive = status.TrialEndDate > DateTime.UtcNow;
                var subscriptionActive = status.SubscriptionEndDate.HasValue && status.SubscriptionEndDate.Value > DateTime.UtcNow;

                if (!trialActive && !subscriptionActive)
                {
                    // Abonelik sayfasına erişime izin ver
                    var path = context.HttpContext.Request.Path.Value?.ToLower() ?? "";
                    if (!path.StartsWith("/api/subscription"))
                    {
                        context.Result = new ObjectResult(new { success = false, message = Messages.TrialExpired }) { StatusCode = 403 };
                        return;
                    }
                }
            }

            await next();
        }

        private sealed class UserStatusCache
        {
            public bool IsBanned { get; init; }
            public string? BanReason { get; init; }
            public string UserType { get; init; } = string.Empty;
            public DateTime TrialEndDate { get; init; }
            public DateTime? SubscriptionEndDate { get; init; }
        }
    }
}
