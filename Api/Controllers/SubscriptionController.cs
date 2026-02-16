using DataAccess.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class SubscriptionController(IUserDal userDal) : BaseApiController
    {
        /// <summary>
        /// Mevcut kullanıcının deneme/abonelik durumunu döner.
        /// Aboneliği bitmiş kullanıcılar da bu endpoint'e erişebilir (UserStatusFilter izin verir).
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            var now = DateTime.UtcNow;
            var trialActive = user.TrialEndDate > now;
            var subscriptionActive = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now;

            string status;
            if (user.IsBanned)
                status = "Banned";
            else if (subscriptionActive)
                status = "Active";
            else if (trialActive)
                status = "Trial";
            else
                status = "Expired";

            return Ok(new
            {
                success = true,
                data = new
                {
                    status,
                    trialEndDate = user.TrialEndDate,
                    subscriptionEndDate = user.SubscriptionEndDate,
                    isBanned = user.IsBanned,
                    banReason = user.BanReason,
                    trialDaysLeft = trialActive ? (int)(user.TrialEndDate - now).TotalDays : 0,
                    subscriptionDaysLeft = subscriptionActive ? (int)(user.SubscriptionEndDate!.Value - now).TotalDays : 0
                }
            });
        }
    }
}
