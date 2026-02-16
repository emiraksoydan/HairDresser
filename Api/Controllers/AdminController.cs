using Business.Resources;
using Core.Extensions;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class AdminController(IUserDal userDal) : BaseApiController
    {
        public record BanRequest(string? Reason);
        public record SetSubscriptionRequest(DateTime EndDate);

        private IActionResult AdminOnly()
        {
            if (!User.ClaimRoles().Contains("Admin"))
                return StatusCode(403, new ErrorResult("Bu işlem için Admin yetkisi gereklidir."));
            return null!;
        }

        /// <summary>
        /// Kullanıcıyı engelle. Admin operation claim'i olan kullanıcılar çağırabilir.
        /// </summary>
        [HttpPost("users/{userId:guid}/ban")]
        public async Task<IActionResult> BanUser(Guid userId, [FromBody] BanRequest request)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var user = await userDal.Get(u => u.Id == userId);
            if (user == null) return BadRequest(new ErrorResult(Messages.UserNotFound));

            user.IsBanned = true;
            user.BanReason = request.Reason;
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);

            return Ok(new SuccessResult(Messages.UserBannedSuccess));
        }

        /// <summary>
        /// Kullanıcı engelini kaldır.
        /// </summary>
        [HttpPost("users/{userId:guid}/unban")]
        public async Task<IActionResult> UnbanUser(Guid userId)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var user = await userDal.Get(u => u.Id == userId);
            if (user == null) return BadRequest(new ErrorResult(Messages.UserNotFound));

            user.IsBanned = false;
            user.BanReason = null;
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);

            return Ok(new SuccessResult(Messages.UserUnbannedSuccess));
        }

        /// <summary>
        /// Kullanıcıya abonelik ekle / uzat.
        /// </summary>
        [HttpPost("users/{userId:guid}/subscription")]
        public async Task<IActionResult> SetSubscription(Guid userId, [FromBody] SetSubscriptionRequest request)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var user = await userDal.Get(u => u.Id == userId);
            if (user == null) return BadRequest(new ErrorResult(Messages.UserNotFound));

            user.SubscriptionEndDate = request.EndDate.ToUniversalTime();
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);

            return Ok(new SuccessResult(Messages.OperationSuccess));
        }
    }
}
