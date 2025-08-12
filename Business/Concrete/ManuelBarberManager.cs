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
using Mapster;

namespace Business.Concrete
{
    public class ManuelBarberManager(IManuelBarberDal manuelBarberDal, IBarberStoreChairDal barberStoreChairDal, IBarberStoreDal barberStoreDal) : IManuelBarberService
    {
        public async Task<IResult> AddAsync(ManuelBarberCreateDto dto, Guid storeOwnerId)
        {
            var entities = dto.Adapt<ManuelBarber>();
            var store = await barberStoreDal.Get(x => x.BarberStoreUserId == storeOwnerId);
            if (store == null)
                return new ErrorResult("Berber dükkanı bulunamadı");
            await manuelBarberDal.Add(entities);
            return new SuccessResult("Manuel berber eklendi.");
        }

        public async Task<IResult> UpdateAsync(ManuelBarberUpdateDto dto, Guid storeOwnerId)
        {
            var entity = await manuelBarberDal.Get(x => x.Id == dto.Id);
            if (entity == null)
                return new ErrorResult("Berber bulunamadı");

            dto.Adapt(entity);
            await manuelBarberDal.Update(entity);
            return new SuccessResult("Berber güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
            var entity = await manuelBarberDal.Get(x => x.Id == id);
            if (entity == null)
                return new ErrorResult("Berber bulunamadı");

            var isAssigned = await barberStoreChairDal.AnyAsync(x => x.ManualBarberId == id);
            if (isAssigned)
                return new ErrorResult("Bu berber bir koltuğa atanmış. Önce koltuk atamasını kaldırın.");

            await manuelBarberDal.Remove(entity);
            return new SuccessResult("Berber silindi.");
        }

        public async Task<IDataResult<List<ManuelBarberDto>>> GetAllByStoreAsync(Guid storeOwnerId)
        {
            var store = await barberStoreDal.Get(x => x.BarberStoreUserId == storeOwnerId);
            if (store == null)
                return new ErrorDataResult<List<ManuelBarberDto>>("Dükkan bulunamadı");

            var list = await manuelBarberDal.GetAll(x => x.IsActive);
            var dto = list.Adapt<List<ManuelBarberDto>>();
            return new SuccessDataResult<List<ManuelBarberDto>>(dto);
        }
    }
}
