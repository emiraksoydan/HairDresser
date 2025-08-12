using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Abstract
{
    public interface IBarberStoreService 
    {
        Task<IResult> Add(BarberStoreCreateDto barberStoreCreateDto,Guid currentUserId);
        Task<IResult> Update(BarberStoreUpdateDto updateDto);
        Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId);
        Task<IDataResult<BarberStoreDetailDto>> GetByIdAsync(Guid id);
        Task<IDataResult<List<BarberStoreDetailDto>>> GetByCurrentUserAsync(Guid currentUserId);
        Task<IDataResult<List<BarberStoreListDto>>> GetNearbyStoresAsync(double lat, double lng, double distance);

        Task<IDataResult<BarberStoreOperationDetail>> GetByIdStoreOperation(Guid id);

    }
}
