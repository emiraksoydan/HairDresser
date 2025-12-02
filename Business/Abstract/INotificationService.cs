using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface INotificationService
    {
        Task<IDataResult<Guid>> CreateAndPushAsync(
        Guid userId,
        NotificationType type,
        Guid? appointmentId,
        string title,
        object payload);

        Task<IDataResult<int>> GetUnreadCountAsync(Guid userId);
        Task<IDataResult<List<NotificationDto>>> GetAllNotify(Guid userId);

        Task<IDataResult<bool>> MarkReadAsync(Guid userId, Guid notificationId);


    }
}
