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
        public Task<List<BarberStoreListDto>> GetNearbyStoresWithStatsAsync(double userLat, double userLng, double maxDistanceKm = 1.0);
        public Task<BarberStoreDetailDto> GetByIdWithStatsAsync(Guid id);
        public Task<BarberStoreOperationDetail> GetByIdStoreOperation(Guid id);
        public Task<List<BarberStoreDetailDto>> GetByCurrentUserWithStatsAsync(Guid userId);

    }
}
