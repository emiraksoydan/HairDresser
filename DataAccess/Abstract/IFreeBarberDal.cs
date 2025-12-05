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
        Task<List<FreeBarberGetDto>> GetNearbyFreeBarberAsync(double lat, double lon, double radiusKm = 1.0);
        Task<FreeBarberMinePanelDto> GetMyPanel(Guid currentUserId);
        Task<FreeBarberMinePanelDetailDto> GetPanelDetailById(Guid panelId);
        Task<FreeBarberMinePanelDto> GetFreeBarberForUsers(Guid freeBarberId);
    }
}
