using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class NotificationManager(INotificationDal notificationDal,
        IBadgeService badgeService,
        IRealTimePublisher realtime) : INotificationService
    {
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> CreateAndPushAsync(Guid userId, NotificationType type, Guid? appointmentId, string title, object payload )
        {
            var n = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AppointmentId = appointmentId,
                Type = type,
                Title = title,
                PayloadJson = JsonSerializer.Serialize(payload),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await notificationDal.Add(n);
            var dto = new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                AppointmentId = n.AppointmentId,
                Title = n.Title,
                PayloadJson = n.PayloadJson,
                CreatedAt = n.CreatedAt
            };
            await realtime.PushNotificationAsync(userId, dto);
            var badges = await badgeService.GetCountsAsync(userId);
            if (badges.Success)
                await realtime.PushBadgeAsync(userId, badges.Data);

            return new SuccessDataResult<Guid>(n.Id);

        }

        public async Task<IDataResult<List<NotificationDto>>> GetAllNotify(Guid userId)
        {
            var list = await notificationDal.GetAll(x => x.UserId == userId);
            var dto = list
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new NotificationDto
                {
                    Id = x.Id,
                    Type = x.Type,
                    AppointmentId = x.AppointmentId,
                    Title = x.Title,
                    PayloadJson = x.PayloadJson,
                    CreatedAt = x.CreatedAt
                }).ToList();

            return new SuccessDataResult<List<NotificationDto>>(dto);
        }
        public async Task<IDataResult<int>> GetUnreadCountAsync(Guid userId)
        {
            var count = (await notificationDal.GetAll(x => x.UserId == userId && x.IsRead == false)).Count;
            return new SuccessDataResult<int>(count);
        }
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> MarkReadAsync(Guid userId, Guid notificationId)
        {
            var n = await notificationDal.Get(x => x.Id == notificationId && x.UserId == userId);
            if (n is null) return new ErrorDataResult<bool>(false, "Bildirim bulunuamadı");

            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await notificationDal.Update(n);

            var badges = await badgeService.GetCountsAsync(userId);
            if (badges.Success)
                await realtime.PushBadgeAsync(userId, badges.Data);

            return new SuccessDataResult<bool>(true);
        }
    }
}
