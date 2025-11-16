using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class SlotManager(IWorkingHourDal workingHourDal, IManuelBarberDal manuelBarberDal, IAppointmentDal
        appointmentDal, IBarberStoreChairDal barberStoreChairDal) : ISlotService
    {

        public async Task<IDataResult<List<WeeklySlotDto>>> GetWeeklySlotsAsync(Guid storeId)
        {
            
            return new SuccessDataResult<List<WeeklySlotDto>>();
        }


    }
}

