using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Business;
using Core.Utilities.Results;
using DataAccess.Abstract;

using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class ManuelBarberManager(IManuelBarberDal manuelBarberDal, IAppointmentService appointmentService, IMapper mapper, IImageService imageService, IBarberStoreChairService barberStoreChairService) : IManuelBarberService
    {
        [ValidationAspect(typeof(ManuelBarberCreateValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> AddAsync(ManuelBarberCreateDto dto)
        {
            
            var barber = mapper.Map<ManuelBarber>(dto);
            await imageService.AddAsync(new CreateImageDto { ImageOwnerId = barber.Id, ImageUrl = dto.ProfileImageUrl, OwnerType = ImageOwnerType.ManuelBarber });
            await manuelBarberDal.Add(barber);

            return new SuccessResult("Manuel berber eklendi.");
        }
        [ValidationAspect(typeof(ManuelBarberUpdateValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UpdateAsync(ManuelBarberUpdateDto dto)
        {
            var barber = await manuelBarberDal.Get(b => b.Id == dto.Id);
            if (barber == null)
                return new ErrorResult("Berber bulunamadı.");

            var hasBlockingAppointments = await appointmentService.AnyControl(barber.Id);
            if (hasBlockingAppointments.Data)
                return new ErrorResult("Bu berberinize ait beklemekte olan veya aktif olan randevu işlemi vardır.");

            var updatedBarber = dto.Adapt(barber);
            await manuelBarberDal.Update(updatedBarber);

            if (!string.IsNullOrWhiteSpace(dto.ProfileImageUrl))
            {
                var getBarberImage = await imageService.GetImage(barber.Id);

                if (getBarberImage.Data == null)
                    await imageService.AddAsync(new CreateImageDto { ImageOwnerId = barber.Id, ImageUrl = dto.ProfileImageUrl, OwnerType = ImageOwnerType.ManuelBarber });
                else if (getBarberImage?.Data.ImageUrl != dto.ProfileImageUrl)
                {
                    await imageService.UpdateAsync(new UpdateImageDto { Id = getBarberImage!.Data.Id, ImageUrl = dto.ProfileImageUrl });
                }
            }
            return new SuccessResult("Berber güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
            var barber = await manuelBarberDal.Get(b => b.Id == id);
            

            if (barber == null)
                return new ErrorResult("Berber bulunamadı.");

            var ruleResult = await BusinessRules.RunAsync(() => CheckBarberHasNoBlockingAppointments(barber.Id),() => CheckBarberNotAssignedToAnyChair(barber.Id));
            if (ruleResult != null && !ruleResult.Success)
                return ruleResult;

            await manuelBarberDal.Remove(barber);
            var getBarberImage = await imageService.GetImage(barber.Id);
            if (getBarberImage.Data != null)
                await imageService.DeleteAsync(getBarberImage.Data.Id);

            return new SuccessResult("Berber silindi.");

        }

        public async Task<IDataResult<List<ManuelBarberDto>>> GetAllByStoreAsync(Guid storeOwnerId)
        {

            return new SuccessDataResult<List<ManuelBarberDto>>();
        }

        public async Task<IResult> AddRangeAsync(List<ManuelBarberCreateDto> list,Guid storeId)
        {
            var manuelBarbers = list.Adapt<List<ManuelBarber>>();
            var imagesToAdd = new List<CreateImageDto>();
            for (int i = 0; i < manuelBarbers.Count; i++)
            {
                var src = list[i];
                var ent = manuelBarbers[i];
                ent.StoreId = storeId;
                if (!string.IsNullOrWhiteSpace(src.ProfileImageUrl))
                {
                    imagesToAdd.Add(new CreateImageDto
                    {
                        ImageOwnerId = ent.Id, 
                        OwnerType = ImageOwnerType.ManuelBarber,
                        ImageUrl = src.ProfileImageUrl,
                    });
                }
            }
            await manuelBarberDal.AddRange(manuelBarbers);
            if (imagesToAdd.Any())
                await imageService.AddRangeAsync(imagesToAdd);

            return new SuccessResult();
        }

        // Helpers Method
        private async Task<IResult> CheckBarberHasNoBlockingAppointments(Guid barberId)
        {
            var hasBlockingAppointments = await appointmentService.AnyControl(barberId);
            if (hasBlockingAppointments.Data)
                return new ErrorResult("Bu berberinize ait beklemekte olan veya aktif olan randevu işlemi vardır.");

            return new SuccessResult();
        }

        private async Task<IResult> CheckBarberNotAssignedToAnyChair(Guid barberId)
        {
            var isAttemptChair = await barberStoreChairService.AttemptBarberControl(barberId);
            if (isAttemptChair.Data)
                return new ErrorResult("Bu berberiniz bir koltuğa atanmış. Önce koltuk ayarını değiştiriniz.");

            return new SuccessResult();
        }

    
    }
}
