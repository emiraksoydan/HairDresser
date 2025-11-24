using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class ServiceOfferingManager(IServiceOfferingDal serviceOfferingDal, IMapper mapper) : IServiceOfferingService
    {
        public async Task<IResult> Add(ServiceOfferingCreateDto serviceOfferingCreateDto, Guid currentUserId)
        {
            var newOffer = mapper.Map<ServiceOffering>(serviceOfferingCreateDto);
            newOffer.OwnerId = currentUserId;
            await serviceOfferingDal.Add(newOffer);
            return new SuccessResult("İşlem başarıyla oluşturuldu.");
        }

        public async Task<IResult> AddRangeAsync(List<ServiceOffering> list)
        {
            await serviceOfferingDal.AddRange(list);
            return new SuccessResult();
        }

        public async Task<IResult> DeleteAsync(Guid Id, Guid currentUserId)
        {
            var offer = await serviceOfferingDal.Get(x => x.Id == Id && x.OwnerId == currentUserId);
            if (offer == null)
                return new ErrorResult("İşlem bulunamadı");
            await serviceOfferingDal.Remove(offer);
            return new SuccessResult("İşlem silindi.");
        }

        public async Task<IDataResult<List<ServiceOfferingGetDto>>> GetAll()
        {
            var offers = await serviceOfferingDal.GetAll();
            var dto = mapper.Map<List<ServiceOfferingGetDto>>(offers);
            return new SuccessDataResult<List<ServiceOfferingGetDto>>(dto);
        }

        public async Task<IDataResult<ServiceOfferingGetDto>> GetByIdAsync(Guid id)
        {
            var offer = await serviceOfferingDal.Get(x => x.Id == id);
            if (offer == null)
                return new ErrorDataResult<ServiceOfferingGetDto>("işlem bulunamadı.");
            var dto = mapper.Map<ServiceOfferingGetDto>(offer);
            return new SuccessDataResult<ServiceOfferingGetDto>(dto);
        }

        public async Task<IDataResult<List<ServiceOfferingGetDto>>> GetServiceOfferingsIdAsync(Guid Id)
        {
            var result = await serviceOfferingDal.GetServiceOfferingsByIdAsync(Id);
            return new SuccessDataResult<List<ServiceOfferingGetDto>>(result);
        }


        public async Task<IResult> Update(ServiceOfferingUpdateDto serviceOfferingUpdateDto)
        {
            var offer = await serviceOfferingDal.Get(x => x.Id == serviceOfferingUpdateDto.Id);
            if (offer == null)
                return new ErrorResult("Güncellenecek işlem bulunamadı.");
            serviceOfferingUpdateDto.Adapt(offer);
            await serviceOfferingDal.Update(offer);
            return new SuccessResult("İşlem güncellendi.");
        }


        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UpdateRange(List<ServiceOfferingUpdateDto> serviceOfferingUpdateDto)
        {
            if (serviceOfferingUpdateDto == null || serviceOfferingUpdateDto.Count == 0)
                return new SuccessResult("Hizmet bulunamadı.");

            var storeId = serviceOfferingUpdateDto[0].OwnerId;

            var existing = await serviceOfferingDal.GetAll(x => x.OwnerId == storeId);

            var dtoIds = serviceOfferingUpdateDto.Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty).Select(d => d.Id!.Value).ToList();

            var updateDtos = serviceOfferingUpdateDto.Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty).ToList();

            var newDtos = serviceOfferingUpdateDto.Where(d => !d.Id.HasValue || d.Id.Value == Guid.Empty).ToList();

            var toDelete = existing.Where(e => !dtoIds.Contains(e.Id)).ToList();

            if (updateDtos.Any())
            {
                var dict = existing.ToDictionary(x => x.Id);

                foreach (var dto in updateDtos)
                {
                    if (!dto.Id.HasValue) continue;

                    if (!dict.TryGetValue(dto.Id.Value, out var entity))
                        continue; 

                    dto.Adapt(entity);   
                }

                await serviceOfferingDal.UpdateRange(existing.Where(e => dtoIds.Contains(e.Id)).ToList());
            }
            if (newDtos.Any())
            {
                var newEntities = newDtos.Adapt<List<ServiceOffering>>();
                foreach (var e in newEntities)
                {
                    if (e.Id == Guid.Empty)
                        e.Id = Guid.NewGuid();
                    e.CreatedAt = DateTime.UtcNow;
                }
                await serviceOfferingDal.AddRange(newEntities);
            }
            if (toDelete.Any())
            {
                await serviceOfferingDal.DeleteAll(toDelete);
            }
            return new SuccessResult("Hizmetler güncellendi.");
        }
    }
}
