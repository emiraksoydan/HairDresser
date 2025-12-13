using Core.Utilities.Results;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IAppointmentNotifyService
    {
        Task<IResult> NotifyAsync(Guid appointmentId, NotificationType type, Guid? actorUserId = null, object? extra = null);
        Task<IResult> NotifyWithAppointmentAsync(Entities.Concrete.Entities.Appointment appointment, NotificationType type, Guid? actorUserId = null, object? extra = null);

    }
}
