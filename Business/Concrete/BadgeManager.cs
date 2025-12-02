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
            var unreadNoti = (await notificationDal.GetAll(x => x.UserId == userId && x.IsRead == false)).Count;

            var threads = await chatThreadDal.GetAll(t =>
                t.CustomerUserId == userId || t.StoreOwnerUserId == userId || t.FreeBarberUserId == userId);

            var unreadMsg = threads.Sum(t =>
                t.CustomerUserId == userId ? t.CustomerUnreadCount :
                t.StoreOwnerUserId == userId ? t.StoreUnreadCount :
                t.FreeBarberUserId == userId ? t.FreeBarberUnreadCount : 0);

            return new SuccessDataResult<BadgeCountDto>(new BadgeCountDto
            {
                UnreadNotifications = unreadNoti,
                UnreadMessages = unreadMsg
            });
        }
    }
}
