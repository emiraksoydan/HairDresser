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
        Task<List<FreeBarberGetDto>> GetNearbyFreeBarberAsync(double lat, double lon, double radiusKm = 1.0, Guid? currentUserId = null);

        // Filtreleme ve arama
        Task<List<FreeBarberGetDto>> GetFilteredFreeBarbersAsync(FilterRequestDto filter);

        Task<FreeBarberMinePanelDto> GetMyPanel(Guid currentUserId);
        Task<FreeBarberMinePanelDetailDto> GetPanelDetailById(Guid panelId);
        Task<FreeBarberMinePanelDto> GetFreeBarberForUsers(Guid freeBarberId);

        /// <summary>
        /// Atomically sets IsAvailable=false only if it is currently true.
        /// Returns true if the lock was acquired (rows affected = 1), false if already locked.
        /// Participates in ambient TransactionScope.
        /// </summary>
        Task<bool> TryLockAsync(Guid freeBarberUserId);
    }
}
