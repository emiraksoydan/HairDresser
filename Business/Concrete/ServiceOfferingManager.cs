using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Abstract;
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
            var offer = await serviceOfferingDal.Get(x=>x.Id == id);
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


        public async Task<IResult> Update(ServiceOfferingUpdateDto serviceOfferingUpdateDto, Guid currentUserId)
        {
            var offer = await serviceOfferingDal.Get(x => x.Id == serviceOfferingUpdateDto.Id && x.OwnerId == currentUserId);
            if (offer == null)
                return new ErrorResult("Güncellenecek işlem bulunamadı.");
            serviceOfferingUpdateDto.Adapt(offer);
            await serviceOfferingDal.Update(offer);
            return new SuccessResult("İşlem güncellendi.");
        }
    }
}
