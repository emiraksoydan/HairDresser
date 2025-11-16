using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class AppointmentManager(IAppointmentDal appointmentDal, IBarberStoreChairDal barberStoreChairDal, INotificationOrchestrator notificationOrchestrator, IServiceOfferingDal serviceOfferingDal, IAppointmentServiceOffering appointmentServiceOffering, IUserDal userDal, IFreeBarberDal freeBarberDal,IBarberStoreDal barberStoreDal) : IAppointmentService
    {
        
    }
}
