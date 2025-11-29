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
        Task<IResult> Update(BarberStoreUpdateDto updateDto,Guid currentUserId);
        Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId);
        Task<IDataResult<BarberStoreDetail>> GetByIdAsync(Guid id);
        Task<IDataResult<List<BarberStoreMineDto>>> GetByCurrentUserAsync(Guid currentUserId);
        Task<IDataResult<List<BarberStoreGetDto>>> GetNearbyStoresAsync(double lat, double lon, double distance);

        Task<IDataResult<BarberStoreMineDto>> GetBarberStoreForUsers(Guid storeId);

    }
}
