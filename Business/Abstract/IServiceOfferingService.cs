using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IServiceOfferingService
    {
        Task<IResult> Add(ServiceOfferingCreateDto serviceOfferingCreateDto, Guid currentUserId);
        Task<IResult> AddRangeAsync(List<ServiceOffering> list);

        Task<IResult> Update(ServiceOfferingUpdateDto serviceOfferingUpdateDto, Guid currentUserId);
        Task<IResult> DeleteAsync(Guid Id, Guid currentUserId);
        Task<IDataResult<ServiceOfferingGetDto>> GetByIdAsync(Guid id);
        Task<IDataResult<List<ServiceOfferingGetDto>>> GetAll();
        Task<IDataResult<List<ServiceOfferingGetDto>>> GetServiceOfferingsIdAsync(Guid Id);


    }
}
