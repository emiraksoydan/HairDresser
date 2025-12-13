using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;


namespace Business.Concrete
{
    public class BadgeManager(INotificationDal notificationDal, IChatThreadDal chatThreadDal) : IBadgeService
    {
        public async Task<IDataResult<BadgeCountDto>> GetCountsAsync(Guid userId)
        {
            // Performance: Use CountAsync instead of GetAll().Count
            var unreadNoti = await notificationDal.CountAsync(x => x.UserId == userId && x.IsRead == false);

            // PERFORMANCE FIX: Database-level sum instead of in-memory sum
            // This avoids loading all threads into memory
            var unreadMsg = await chatThreadDal.GetUnreadMessageCountAsync(userId);

            return new SuccessDataResult<BadgeCountDto>(new BadgeCountDto
            {
                UnreadNotifications = unreadNoti,
                UnreadMessages = unreadMsg
            });
        }
    }
}
