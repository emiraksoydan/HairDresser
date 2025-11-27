using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;
using MapsterMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Business.Concrete
{
    public class FreeBarberManager(IFreeBarberDal freeBarberDal,IAppointmentService _appointmentService,IImageService _imageService, IServiceOfferingService _serviceOfferingService) : IFreeBarberService
    {
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        [ValidationAspect(typeof(FreeBarberDtoValidator))]
        public async Task<IResult> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId)
        {
            var entity = freeBarberCreateDto.Adapt<FreeBarber>();
            entity.FreeBarberUserId = currentUserId;
            await freeBarberDal.Add(entity);
            await SaveFreeBarberImagesAsync(freeBarberCreateDto, entity.Id);
            await SaveOfferingsAsync(freeBarberCreateDto, entity.Id);
            return new SuccessResult("Serbest berber portalı başarıyla oluşturuldu.");
        }
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        [ValidationAspect(typeof(FreeBarberDtoValidator))]
        public async Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto,Guid currentUserId)
        {
            var existingEntity = await freeBarberDal.Get(x=>x.Id == freeBarberUpdateDto.Id);
            if (existingEntity.FreeBarberUserId != currentUserId)
                return new ErrorResult("Bu paneli güncellemeye yetkiniz yoktur.");
            var appointCont = await _appointmentService.AnyControl(freeBarberUpdateDto.Id);
            if(appointCont.Data)
                return new ErrorResult("Randevu işleminiz bulunmaktadır. Lütfen işlemden sonra güncelleyiniz");

            freeBarberUpdateDto.Adapt(existingEntity);
            await freeBarberDal.Update(existingEntity);
            await _imageService.UpdateRangeAsync(freeBarberUpdateDto.ImageList);
            await _serviceOfferingService.UpdateRange(freeBarberUpdateDto.Offerings);
            return new SuccessResult("Serbest berber güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid storeId)
        {

            return new SuccessResult("Serbest berber silindi.");
        }

        public async Task<IDataResult<FreeBarberMinePanelDto>> GetMyPanel(Guid currentUserId)
        {
            var result = await freeBarberDal.GetMyPanel(currentUserId);
            if (result == null) {
                return new ErrorDataResult<FreeBarberMinePanelDto>("Panel getirilemedi");
            }
            return new SuccessDataResult<FreeBarberMinePanelDto>(result);
        }

        public async Task<IDataResult<List<FreeBarberGetDto>>> GetNearbyFreeBarberAsync(double lat, double lon, double distance)
        {
            var getFreeBarberResult = await freeBarberDal.GetNearbyFreeBarberAsync(lat, lon, distance);
            return new SuccessDataResult<List<FreeBarberGetDto>>(getFreeBarberResult);
        }

        public async Task<IDataResult<FreeBarberMinePanelDetailDto>> GetMyPanelDetail(Guid panelId)
        {
            var result = await freeBarberDal.GetPanelDetailById(panelId);
            if (result == null) {
                return new ErrorDataResult<FreeBarberMinePanelDetailDto>("Panel detayı getirilemedi");
            }
            return new SuccessDataResult<FreeBarberMinePanelDetailDto>(result);
        }

        private async Task SaveFreeBarberImagesAsync(FreeBarberCreateDto dto, Guid panelId)
        {
            if (dto.ImageList?.Count > 0)
            {
                foreach (var itemImage in dto.ImageList)
                {
                    itemImage.ImageOwnerId = panelId;

                }
                await _imageService.AddRangeAsync(dto.ImageList);
            }
        }

        private async Task SaveOfferingsAsync(FreeBarberCreateDto dto, Guid panelId)
        {
            var offers = (dto.Offerings ?? new List<ServiceOfferingCreateDto>()).Adapt<List<ServiceOffering>>();

            if (offers != null && offers.Count > 0)
            {
                foreach (var o in offers)
                    o.OwnerId = panelId;
                await _serviceOfferingService.AddRangeAsync(offers);
            }
        }
    }
}
