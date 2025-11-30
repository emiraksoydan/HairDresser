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
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class AppointmentManager(IAppointmentDal appointmentDal) : IAppointmentService
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

        public async Task<IDataResult<List<ChairSlotDto>>> GetAvailibity(Guid storeId, DateOnly dateOnly, CancellationToken ct = default)
        {
            var res = await appointmentDal.GetAvailibilitySlot(storeId, dateOnly, ct);
            return new SuccessDataResult<List<ChairSlotDto>>(res);
        }

        private static (bool reqStore, bool reqWorker) ComputeApprovals(AppointmentRequester requestedBy, Guid? appointmentToId, Guid? workerUserId)
        {
            // appointmentToId store userId ise store onayı fikri çalışır (store->free senaryosunda reqStore=false ayarlıyoruz)
            var reqStore = appointmentToId != null && requestedBy != AppointmentRequester.Store;

            // worker/free barber onayı (worker varsa ve requester worker değilse)
            var reqWorker = workerUserId != null && requestedBy != AppointmentRequester.FreeBarber;

            return (reqStore, reqWorker);
        }



    }
}
