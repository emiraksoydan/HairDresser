
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;

using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class BarberStoreChairManager(IBarberStoreChairDal barberStoreChairDal,IBarberStoreDal barberStoreDal,IAppointmentDal appointmentDal, IMapper mapper) : IBarberStoreChairService
    {
        public async Task<IResult> AddAsync(BarberChairCreateDto dto, Guid currentUserId)
        {
           
            return new SuccessResult("Koltuk başarıyla oluşturuldu.");
        }

        public async Task<IResult> DeleteAsync(Guid chairId)
        {
            
            return new SuccessResult("Koltuk silindi.");
        }

        public async Task<IDataResult<List<BarberChairDto>>> GetAllByStoreAsync(Guid storeId)
        {
            
            return new SuccessDataResult<List<BarberChairDto>>();
        }

        public async Task<IDataResult<BarberChairDto>> GetChairById(Guid chairId)
        {
            
            return new SuccessDataResult<BarberChairDto>();


        }

        public async Task<IResult> UpdateAsync(BarberChairUpdateDto dto)
        {
            
            return new SuccessResult("Koltuk güncellendi.");
        }
    }
}
