using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class AppointmentManager(IAppointmentDal appointmentDal,IBarberStoreDal barberStoreDal,IManuelBarberDal manuelBarberDal) : IAppointmentService
    {
        public async Task<IDataResult<bool>> AnyControl(Guid id)
        {
            var hasBlocking = await appointmentDal.AnyAsync(x=>(x.FreeBarberUserId ==  id || x.ManuelBarberId == id) && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyChairControl(Guid id)
        {
         
            var hasBlocking = await appointmentDal.AnyAsync(x => x.ChairId == id && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasBlocking);
        }

        public async Task<IDataResult<bool>> AnyStoreControl(Guid id)
        {
            var getBarberStore = await barberStoreDal.Get(x => x.Id == id);
            var hasStoreApp = await appointmentDal.AnyAsync(x => (x.BarberStoreUserId == getBarberStore.BarberStoreOwnerId)  && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));

            return new SuccessDataResult<bool>(hasStoreApp);
        }

        public async Task<IDataResult<List<ChairSlotDto>>> GetAvailibity(Guid storeId, DateOnly dateOnly, CancellationToken ct = default)
        {
            var res = await appointmentDal.GetAvailibilitySlot(storeId, dateOnly, ct);
            return new SuccessDataResult<List<ChairSlotDto>>(res);
        }

    }
}
