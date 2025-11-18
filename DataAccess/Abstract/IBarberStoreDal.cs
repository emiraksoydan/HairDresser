using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IBarberStoreDal : IEntityRepository<BarberStore>
    {
        Task<List<BarberStoreGetDto>> GetNearbyStoresAsync(double lat, double lon, double radiusKm = 1.0);

        Task<List<BarberStoreMineDto>> GetMineStores(Guid currentUserId);

        Task<BarberStoreDetail> GetByIdStore(Guid storeId);

    }
}
