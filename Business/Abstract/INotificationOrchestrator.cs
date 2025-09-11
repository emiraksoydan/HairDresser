using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface INotificationOrchestrator
    {
        Task CustomerToStoreRequestedAsync(Appointment appt, Guid actorUserId,List<AppointmentServiceOffering> snapshotItems);
        Task StoreInvitesBarberAsync(Appointment appt, Guid storeOwnerUserId, Guid actorUserId);
        Task FreeBarberToStoreAsync(Appointment appt, Guid actorUserId);
        Task ApprovalDecisionAsync(Appointment appt, UserType byRole, bool approve);
    }
}
