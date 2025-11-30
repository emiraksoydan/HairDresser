using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Enums
{
    public enum NotificationType
    {
        AppointmentPending = 1,
        AppointmentApproved = 2,
        AppointmentRejected = 3,
        AppointmentExpired = 4,
        AppointmentCancelled = 5,
        AppointmentUpdated = 6
    }
}
