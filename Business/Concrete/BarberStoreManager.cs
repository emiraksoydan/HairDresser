
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
    public class BarberStoreManager(IBarberStoreDal barberStoreDal, IWorkingHourService workingHourService, IManuelBarberService _manuelBarberService, IBarberStoreChairService _barberStoreChairService, IServiceOfferingService _serviceOfferingService, IAppointmentService appointmentService, IImageService _imageService) : IBarberStoreService
    {
        //[SecuredOperation("BarberStore.Add")]
        [ValidationAspect(typeof(BarberStoreCreateDtoValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> Add(BarberStoreCreateDto dto, Guid currentUserId)
        {
            IResult result = BusinessRules.Run(BarberAttemptCore(dto.Chairs,c=>c.BarberId));
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
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> Update(BarberStoreUpdateDto dto, Guid currentUserId)
        {
            IResult result = BusinessRules.Run(BarberAttemptCore(dto.Chairs, c => c.BarberId.ToString()));
            if (result != null)
                return result;
            var anyAppointCt = await appointmentService.AnyStoreControl(dto.Id);
            if (anyAppointCt.Data)
                return new ErrorResult("Bu dükkana ait aktif veya bekleyen randevu var önce müsait olmalısınız ");
         
            BarberStore store = dto.Adapt<BarberStore>();
            store.BarberStoreOwnerId = currentUserId;
            await barberStoreDal.Update(store);
            await _imageService.UpdateRangeAsync(dto.StoreImageList);
            await _serviceOfferingService.UpdateRange(dto.Offerings);
            await workingHourService.UpdateRangeAsync(dto.WorkingHours);

            return new SuccessResult("Berber dükkanı başarıyla güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId)
        {

            return new SuccessResult("Dükkan silindi.");
        }

        public async Task<IDataResult<BarberStoreDetail>> GetByIdAsync(Guid id)
        {
            var result = await barberStoreDal.GetByIdStore(id);
            return new SuccessDataResult<BarberStoreDetail>(result);
        }

        public async Task<IDataResult<List<BarberStoreMineDto>>> GetByCurrentUserAsync(Guid currentUserId)
        {
            var result = await barberStoreDal.GetMineStores(currentUserId);
            return new SuccessDataResult<List<BarberStoreMineDto>>(result);
        }

        public async Task<IDataResult<List<BarberStoreGetDto>>> GetNearbyStoresAsync(double lat, double lon, double distance)
        {
            var result = await barberStoreDal.GetNearbyStoresAsync(lat, lon, distance);
            return new SuccessDataResult<List<BarberStoreGetDto>>(result, "1 Kilometreye sınırdaki berberler getirildi");
        }



        //private IResult BarberAttemptCreateControl(List<BarberChairCreateDto> chairList)
        //{
        //    var assigned = chairList.Select((c, i) => new { Index = i, Chair = c }).Where(x => x.Chair?.BarberId != null &&
        //        !(x.Chair.BarberId is string s && string.IsNullOrWhiteSpace(s))).ToList();
        //    var duplicates = assigned
        //        .GroupBy(x => x.Chair.BarberId).Where(g => g.Count() > 1).Select(g => new
        //        {
        //            BarberId = g.Key,
        //            Chairs = g.Select(x => x.Index).ToList(),
        //            Count = g.Count()
        //        }).ToList();
        //    if (duplicates.Count > 0)
        //    {
        //        return new ErrorResult("Bir berber birden fazla koltuğa atanamaz.");
        //    }
        //    return new SuccessResult();
        //}

        private IResult BarberAttemptCore<TChair>(List<TChair>? chairList,Func<TChair, string?> getBarberId)
        {
            if (chairList == null || chairList.Count == 0)
                return new SuccessResult();

            // BerberId'si dolu olan koltukları al
            var assigned = chairList
                .Select((c, i) => new { Index = i, BarberId = getBarberId(c) })
                .Where(x => !string.IsNullOrWhiteSpace(x.BarberId))
                .ToList();

            // Aynı berber birden fazla koltuğa atanmış mı?
            var duplicates = assigned
                .GroupBy(x => x.BarberId)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    BarberId = g.Key,
                    Chairs = g.Select(x => x.Index).ToList(),
                    Count = g.Count()
                })
                .ToList();

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
                foreach (var itemImage in dto.StoreImageList)
                {
                    itemImage.ImageOwnerId = storeId;

                }
                await _imageService.AddRangeAsync(dto.StoreImageList);
            }
        }

        private async Task SaveManuelBarbersAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var manuelBarberDtos = dto.ManuelBarbers;
            if (manuelBarberDtos?.Count == 0)
                return;
            await _manuelBarberService.AddRangeAsync(manuelBarberDtos!, storeId);
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
                await _serviceOfferingService.AddRangeAsync(offers);
            }
        }

        private async Task SaveChairsAsync(BarberStoreCreateDto dto, Guid storeId)
        {
            var chairs = (dto.Chairs ?? new List<BarberChairCreateDto>()).Adapt<List<BarberChair>>();
            if (chairs != null && chairs.Count > 0)
            {
                foreach (var c in chairs)
                    c.StoreId = storeId;
                await _barberStoreChairService.AddRangeAsync(chairs);
            }
        }



    }
}
