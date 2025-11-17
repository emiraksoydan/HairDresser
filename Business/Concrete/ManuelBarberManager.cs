using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;

namespace Business.Concrete
{
    public class ManuelBarberManager(IManuelBarberDal manuelBarberDal, IBarberStoreChairDal barberStoreChairDal, IBarberStoreDal barberStoreDal,IAppointmentDal appointmentDal) : IManuelBarberService
    {
        public async Task<IResult> AddAsync(ManuelBarberCreateDto dto, Guid storeOwnerId)
        {
            
            return new SuccessResult("Manuel berber eklendi.");
        }

        public async Task<IResult> UpdateAsync(ManuelBarberUpdateDto dto)
        {
            
            return new SuccessResult("Berber güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
           
            return new SuccessResult("Berber silindi.");
        }

        public async Task<IDataResult<List<ManuelBarberDto>>> GetAllByStoreAsync(Guid storeOwnerId)
        {
            
            return new SuccessDataResult<List<ManuelBarberDto>>();
        }

        public async Task<IResult> AddRangeAsync(List<ManuelBarber> list)
        {
             await manuelBarberDal.AddRange(list);
             return new SuccessResult();
        }
    }
}
