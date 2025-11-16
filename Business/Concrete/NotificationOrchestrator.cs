using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using Business.Abstract;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class NotificationOrchestrator(INotificationService _notif, INotificationDal notificationDal, IAppointmentDal appointmentDal, IBarberStoreDal barberStoreDal, IFreeBarberDal freeBarberDal, IUserDal userDal, IBarberStoreChairDal _chair, IManuelBarberDal manuelBarberDal) : INotificationOrchestrator
    {

        public async Task CustomerToStoreRequestedAsync(Appointment appt, Guid actorUserId, List<AppointmentServiceOffering> snapshotItems)
        {
          
        }

        public async Task StoreInvitesBarberAsync(Appointment appt, Guid storeOwnerUserId, Guid actorUserId)
        {

            
        }

        public async Task ApprovalDecisionAsync(Appointment appt, UserType byRole, bool approve)
        {
            
        }

        public async Task FreeBarberToStoreAsync(Appointment appt, Guid actorUserId)
        {
            
        }
    }
}
