using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IServiceOfferingService
    {
        Task<IResult> Add(ServiceOfferingCreateDto serviceOfferingCreateDto, Guid currentUserId);
        Task<IResult> Update(ServiceOfferingUpdateDto serviceOfferingUpdateDto, Guid currentUserId);
        Task<IResult> DeleteAsync(Guid Id, Guid currentUserId);
        Task<IDataResult<ServiceOfferingListDto>> GetByIdAsync(Guid id);
        Task<IDataResult<List<ServiceOfferingListDto>>> GetAll();
        Task<IDataResult<List<ServiceOfferingListDto>>> GetServiceOfferingsIdAsync(Guid Id);


    }
}
