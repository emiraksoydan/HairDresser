
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class BarberStoreChairManager(IBarberStoreChairDal barberStoreChairDal,IMapper mapper) : IBarberStoreChairService
    {
        public async Task<IResult> AddAsync(BarberChairCreateDto dto)
        {
           
            return new SuccessResult("Koltuk başarıyla oluşturuldu.");
        }

        public async Task<IResult> AddRangeAsync(List<BarberChair> list)
        {

            await barberStoreChairDal.AddRange(list);
            return new SuccessResult();
        }

        public async Task<IDataResult<bool>> AttemptBarberControl(Guid id)
        {
            var hasAttempt = await barberStoreChairDal.AnyAsync(x => x.ManuelBarberId == id);
            return new SuccessDataResult<bool>(hasAttempt);
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
            
            return new SuccessResult("Koltuk silindi.");
        }

        public async Task<IDataResult<List<BarberChairDto>>> GetAllByStoreAsync(Guid storeId)
        {
            
            return new SuccessDataResult<List<BarberChairDto>>();
        }

        public async Task<IDataResult<BarberChairDto>> GetById(Guid id)
        {
            return new SuccessDataResult<BarberChairDto>();
        }

        public async Task<IResult> UpdateAsync(BarberChairUpdateDto dto)
        {
            
            return new SuccessResult("Koltuk güncellendi.");
        }
    }
}
