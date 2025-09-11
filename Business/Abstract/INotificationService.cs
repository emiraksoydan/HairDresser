using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface INotificationService
    {
        Task<IDataResult<Guid>> CreateAsync(Guid userId, NotificationType type, Guid correlationId, object payload, string topic = "Appointment");
        Task<IDataResult<int>> GetUnreadCountAsync(Guid userId);

        Task<IDataResult<List<Notification>>> GetAllNotify(Guid userId);


    }
}
