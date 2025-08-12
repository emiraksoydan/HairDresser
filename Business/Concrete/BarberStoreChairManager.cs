using System;
using System.Collections.Generic;
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
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class BarberStoreChairManager(IBarberStoreChairDal barberStoreChairDal,IBarberStoreDal barberStoreDal,IAppointmentDal appointmentDal, IMapper mapper) : IBarberStoreChairService
    {
        public async Task<IResult> AddAsync(BarberChairCreateDto dto, Guid currentUserId)
        {
            var entities = dto.Adapt<BarberChair>();
            var store = await barberStoreDal.Get(x => x.BarberStoreUserId == currentUserId);
            entities.StoreId = store.Id;
    
            await barberStoreChairDal.Add(entities);
            return new SuccessResult("Koltuk başarıyla oluşturuldu.");
        }

        public async Task<IResult> DeleteAsync(Guid chairId)
        {

            var storeChair = await barberStoreChairDal.Get(x => x.Id == chairId);
            if (storeChair == null)
                return new ErrorResult("Koltuk bulunamadı");

            var hasActiveAppointment = await appointmentDal.AnyAsync(x =>
            x.ChairId == chairId &&
            x.Status != AppointmentStatus.Completed &&
            x.Status != AppointmentStatus.Cancelled);
            if (hasActiveAppointment)
            {   
                return new ErrorResult("Koltuğunuza ait aktif randevu bulunduğu için koltuk silinemez");
            }
            await barberStoreChairDal.Remove(storeChair);
            return new SuccessResult("Koltuk silindi.");
        }

        public async Task<IDataResult<List<BarberChairDto>>> GetAllByStoreAsync(Guid storeId)
        {
            var result = await barberStoreChairDal.GetAll(x=>x.StoreId == storeId);
            var dto = mapper.Map<List<BarberChairDto>>(result);
            return new SuccessDataResult<List<BarberChairDto>>(dto);
        }

        public async Task<IResult> UpdateAsync(BarberChairUpdateDto dto)
        {
            var store = await barberStoreChairDal.Get(x => x.Id == dto.Id);
            if (store == null)
                return new ErrorResult("Güncellenecek koltuk bulunamadı.");

            dto.Adapt(store);
            await barberStoreChairDal.Update(store);
            return new SuccessResult("Koltuk güncellendi.");
        }
    }
}
