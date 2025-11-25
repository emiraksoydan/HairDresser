using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IFreeBarberService
    {
        Task<IResult> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId);
        Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto);
        Task<IResult> DeleteAsync(Guid storeId);
        Task<IDataResult<FreeBarberDetailDto>> GetByIdAsync(Guid id);
        Task<IDataResult<List<FreeBarberGetDto>>> GetNearbyFreeBarberAsync(double lat, double lon, double distance);
        Task<IDataResult<FreeBarberDetailDto>> GetMyPanel(Guid currentUserId); 
    }
}
