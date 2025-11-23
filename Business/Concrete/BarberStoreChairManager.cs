
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Business;
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
    public class BarberStoreChairManager(IBarberStoreChairDal barberStoreChairDal,IAppointmentService appointmentService, IMapper mapper) : IBarberStoreChairService
    {

        [ValidationAspect(typeof(BarberStoreChairCreateValidator))]
        public async Task<IResult> AddAsync(BarberChairCreateDto dto)
        {
            Guid? barberId = null;
            if (!string.IsNullOrWhiteSpace(dto.BarberId))
            {
                if (!Guid.TryParse(dto.BarberId, out var parsed))
                {
                    return new ErrorResult("Berber Id formatı hatalı.");
                }
                barberId = parsed;
            }

            var ruleResult = await BusinessRules.RunAsync(() => EnsureBarberNotAssignedToAnotherChairAsync(barberId, null));

            if (ruleResult != null)  
                return ruleResult;

            var barberChair = mapper.Map<BarberChair>(dto);
            await barberStoreChairDal.Add(barberChair);

            return new SuccessResult("Koltuk başarıyla oluşturuldu.");
        }

        public async Task<IResult> AddRangeAsync(List<BarberChair> list)
        {

            await barberStoreChairDal.AddRange(list);
            return new SuccessResult();
        }
        [ValidationAspect(typeof(BarberStoreChairUpdateValidator))]
        public async Task<IResult> UpdateAsync(BarberChairUpdateDto dto)
        {
            var ruleResult = await BusinessRules.RunAsync(() => EnsureBarberNotAssignedToAnotherChairAsync(dto.BarberId, dto.Id));

            if (ruleResult != null)
                return ruleResult;

            var barberChair = await barberStoreChairDal.Get(b => b.Id == dto.Id);
            if (barberChair == null)
                return new ErrorResult("Koltuk bulunamadı.");

            var hasBlockingAppointments = await appointmentService.AnyChairControl(barberChair.Id);
            if (hasBlockingAppointments.Data)
                return new ErrorResult("Bu koltuğa ait beklemekte olan veya aktif olan randevu işlemi vardır.");

            var updatedChair = dto.Adapt(barberChair);
            await barberStoreChairDal.Update(updatedChair);

            return new SuccessResult("Koltuk güncellendi.");
        }
        public async Task<IDataResult<bool>> AttemptBarberControl(Guid id)
        {
            var hasAttempt = await barberStoreChairDal.AnyAsync(x => x.ManuelBarberId == id);
            return new SuccessDataResult<bool>(hasAttempt);
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
            var chair = await barberStoreChairDal.Get(b => b.Id == id);
            if (chair == null)
                return new ErrorResult("Koltuk bulunamadı.");

            await barberStoreChairDal.Remove(chair);
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

        private async Task<IResult> EnsureBarberNotAssignedToAnotherChairAsync(Guid? barberId, Guid? currentChairId = null)
        {
          
            if (barberId is null)
                return new SuccessResult();

            var exists = await barberStoreChairDal.Get(c =>
                c.ManuelBarberId == barberId && 
                (currentChairId == null || c.Id != currentChairId) 
            );

            if (exists != null)
                return new ErrorResult("Bu berber zaten başka bir koltuğa atanmış.");

            return new SuccessResult();
        }


    }
}
