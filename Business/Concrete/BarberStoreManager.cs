
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Transaction;
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
using System;
using Twilio.TwiML.Messaging;

namespace Business.Concrete
{
    public class BarberStoreManager(IBarberStoreDal barberStoreDal, IWorkingHourService workingHourService, IManuelBarberDal _manuelBarberDal, IBarberStoreChairDal barberStoreChairDal, IServiceOfferingDal serviceOfferingDal, IAppointmentDal appointmentDal, IImageDal imageDal) : IBarberStoreService
    {
        //[SecuredOperation("BarberStore.Add")]
        [ValidationAspect(typeof(BarberStoreCreateDtoValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> Add(BarberStoreCreateDto dto, Guid currentUserId)
        {
            IResult result = BusinessRules.Run(BarberAppointmentControl(dto.ManuelBarbers, dto.Chairs));
            if (result != null)
                return result;

            var store = await CreateStoreAsync(dto, currentUserId);
            await SaveStoreImagesAsync(dto, store.Id);
            await SaveManuelBarbersAsync(dto, store.Id);
            await SaveChairsAsync(dto, store.Id);
            await SaveOfferingsAsync(dto, store.Id);
            await SaveWorkingHoursAsync(dto, store.Id);
            return new SuccessResult("Berber dükkanı başarıyla oluşturuldu.");
        }

        [ValidationAspect(typeof(BarberStoreUpdateDtoValidator))]
        public async Task<IResult> Update(BarberStoreUpdateDto dto)
        {

            return new SuccessResult("Berber dükkanı başarıyla güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId)
        {

            return new SuccessResult("Dükkan silindi.");
        }

        public async Task<IDataResult<BarberStoreDetailDto>> GetByIdAsync(Guid id)
        {

            return new SuccessDataResult<BarberStoreDetailDto>();
        }

        public async Task<IDataResult<List<BarberStoreDetailDto>>> GetByCurrentUserAsync(Guid currentUserId)
        {

            return new SuccessDataResult<List<BarberStoreDetailDto>>();
        }

        public async Task<IDataResult<List<BarberStoreGetDto>>> GetNearbyStoresAsync(double lat, double lon, double distance)
        {
            var result = await barberStoreDal.GetNearbyStoresAsync(lat, lon, distance);
            return new SuccessDataResult<List<BarberStoreGetDto>>(result, "1 Kilometreye sınırdaki berberler getirildi");
        }

        public async Task<IDataResult<BarberStoreOperationDetail>> GetByIdStoreOperation(Guid id)
        {

            return new SuccessDataResult<BarberStoreOperationDetail>();
        }


        private IResult BarberAppointmentControl(List<ManuelBarberCreateDto> manuelBarberList, List<BarberChairCreateDto> chairList)
        {
            var assigned = chairList.Select((c, i) => new { Index = i, Chair = c }).Where(x => x.Chair?.BarberId != null &&
                !(x.Chair.BarberId is string s && string.IsNullOrWhiteSpace(s))).ToList();
            var duplicates = assigned
                .GroupBy(x => x.Chair.BarberId).Where(g => g.Count() > 1).Select(g => new
                {
                    BarberId = g.Key,
                    Chairs = g.Select(x => x.Index).ToList(),
                    Count = g.Count()
                }).ToList();
            if (duplicates.Count > 0)
            {
                return new ErrorResult("Bir berber birden fazla koltuğa atanamaz.");
            }
            return new SuccessResult();
        }

        private async Task<BarberStore> CreateStoreAsync(BarberStoreCreateDto dto, Guid currentUserId)
        {
            BarberStore store = dto.Adapt<BarberStore>();
            store.BarberStoreOwnerId = currentUserId;
            await barberStoreDal.Add(store);
            return store;
        }

        private async Task SaveStoreImagesAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            if (dto.StoreImageList?.Count > 0)
            {
                var barberStoreImages = dto.StoreImageList.Adapt<List<Image>>();
                foreach (var itemImage in barberStoreImages)
                {
                    itemImage.ImageOwnerId = storeId;
                    itemImage.OwnerType = ImageOwnerType.Store;
                }
                await imageDal.AddRange(barberStoreImages);
            }
        }

        private async Task SaveManuelBarbersAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var manuelBarbers = (dto.ManuelBarbers ?? new List<ManuelBarberCreateDto>()).Adapt<List<ManuelBarber>>();


            if (manuelBarbers.Any())
            {
                var imagesToAdd = new List<Image>();
                for (int i = 0; i < manuelBarbers.Count; i++)
                {
                    var src = dto.ManuelBarbers![i];
                    var ent = manuelBarbers[i];
                    ent.StoreId = storeId;
                    if (!string.IsNullOrWhiteSpace(src.ProfileImageUrl))
                    {
                        var img = new Image
                        {
                            ImageOwnerId = ent.Id,
                            OwnerType = ImageOwnerType.ManuelBarber,
                            ImageUrl = src.ProfileImageUrl,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        };
                        imagesToAdd.Add(img);
                    }
                }
                await imageDal.AddRange(imagesToAdd);
                await _manuelBarberDal.AddRange(manuelBarbers);
            }
        }

        private async Task SaveWorkingHoursAsync(BarberStoreCreateDto dto, Guid storeId)
        {

            var workingHours = (dto.WorkingHours ?? new List<WorkingHourCreateDto>()).Adapt<List<WorkingHour>>();
            if (workingHours.Count > 0)
            {
                foreach (var workingHour in workingHours)
                    workingHour.OwnerId = storeId;
                await workingHourService.AddRangeAsync(workingHours);
            }
        }

        private async Task SaveOfferingsAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var offers = (dto.Offerings ?? new List<ServiceOfferingCreateDto>()).Adapt<List<ServiceOffering>>();

            if (offers != null && offers.Count > 0)
            {
                foreach (var o in offers)
                    o.OwnerId = storeId;
                await serviceOfferingDal.AddRange(offers);
            }
        }

        private async Task SaveChairsAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var chairs = (dto.Chairs ?? new List<BarberChairCreateDto>()).Adapt<List<BarberChair>>();
            if (chairs != null && chairs.Count > 0)
            {
                foreach (var c in chairs)
                    c.StoreId = storeId;
                await barberStoreChairDal.AddRange(chairs);
            }
        }



    }
}
