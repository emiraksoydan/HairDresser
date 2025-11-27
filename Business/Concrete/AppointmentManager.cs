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
    public class AppointmentManager(IAppointmentDal appointmentDal, IBarberStoreChairDal barberStoreChairDal, INotificationOrchestrator notificationOrchestrator, IServiceOfferingDal serviceOfferingDal, IAppointmentServiceOffering appointmentServiceOffering, IUserDal userDal, IFreeBarberDal freeBarberDal) : IAppointmentService
    {
        public async Task<IDataResult<bool>> AnyControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x=>(x.WorkerUserId ==  id || x.AppointmentFromId == id || x.AppointmentToId == id) && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));
     
            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyChairControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x => x.ChairId == id && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyStoreControl(Guid id)
        {
            var hasStoreApp = await appointmentDal.AnyAsync(x => (x.AppointmentFromId == id || x.AppointmentToId == id)  && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasStoreApp);
        }
    }
}
