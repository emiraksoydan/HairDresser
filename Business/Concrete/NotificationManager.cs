using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class NotificationManager(INotificationDal notificationDal) : INotificationService
    {
        public async Task<IDataResult<Guid>> CreateAsync(Guid userId, NotificationType type,Guid correlationId, object payload, string topic = "AppointmentRequest")
        {
            var n = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Topic = topic,
                Payload = JsonSerializer.Serialize(payload),
                IsRead = false,
                CorrelationId = correlationId,
                CreatedAtUtc = DateTime.Now
            };
            await notificationDal.Add(n);
            var unread = await notificationDal.GetUnreadCountAsync(userId);
            return new SuccessDataResult<Guid>(n.UserId);
        }

        public async Task<IDataResult<List<Notification>>> GetAllNotify(Guid userId)
        {
            var getNotifyList = await notificationDal.GetAll(x=>x.UserId == userId);
            return new SuccessDataResult<List<Notification>>(getNotifyList);
        }

        public async Task<IDataResult<int>> GetUnreadCountAsync(Guid userId)
        {
            var result = await notificationDal.GetUnreadCountAsync(userId);
            return new SuccessDataResult<int>(result);
        }
    }
}
