using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Validation;
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
    public class FreeBarberManager(IFreeBarberDal freeBarberDal, IWorkingHourDal workingHourDal, IServiceOfferingDal serviceOfferingDal, IAppointmentDal appointmentDal, IMapper _mapper) : IFreeBarberService
    {
        [ValidationAspect(typeof(FreeBarberCreateDtoValidator))]
        public async Task<IResult> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId)
        {
           
            return new SuccessResult("Serbest berber portalı başarıyla oluşturuldu.");
        }
        [ValidationAspect(typeof(FreeBarberCreateDtoValidator))]
        public async Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto)
        {
            
            return new SuccessResult("Serbest berber güncellendi.");
        }

        public async Task<IResult> DeleteAsync(Guid storeId)
        {
            
            return new SuccessResult("Serbest berber silindi.");
        }

        public async Task<IDataResult<FreeBarberDetailDto>> GetByIdAsync(Guid id)
        {
           
            return new SuccessDataResult<FreeBarberDetailDto>();
        }

        public async Task<IDataResult<FreeBarberDetailDto>> GetMyPanel(Guid currentUserId)
        {
           
            return new SuccessDataResult<FreeBarberDetailDto>();
        }

        public async Task<IDataResult<List<FreeBarberListDto>>> GetNearbyStoresAsync(double lat, double lng, double distance)
        {
         
            return new SuccessDataResult<List<FreeBarberListDto>>();
        }

    }
}
