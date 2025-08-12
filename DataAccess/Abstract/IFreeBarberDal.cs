using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IFreeBarberDal : IEntityRepository<FreeBarber>
    {
        Task<FreeBarberDetailDto?> GetByFreeBarberPanel(Guid userId);
        Task<FreeBarberDetailDto?> GetByIdWithStatsAsync(Guid id);
        Task<List<FreeBarberListDto>> GetNearbyFreeBarberWithStatsAsync(double userLat, double userLng, double maxDistanceKm = 1);

    }
}
