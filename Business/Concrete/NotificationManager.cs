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
          
            return new SuccessDataResult<Guid>();
        }

        public async Task<IDataResult<List<Notification>>> GetAllNotify(Guid userId)
        {
            return new SuccessDataResult<List<Notification>>();
        }

        public async Task<IDataResult<int>> GetUnreadCountAsync(Guid userId)
        {
            return new SuccessDataResult<int>();
        }
    }
}
