using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IFreeBarberService
    {
        Task<IDataResult<Guid>> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId);
        Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto, Guid currentUserId);
        Task<IResult> DeleteAsync(Guid panelId);
        Task<IDataResult<List<FreeBarberGetDto>>> GetNearbyFreeBarberAsync(double lat, double lon, double distance, Guid? currentUserId = null);
        
        // Filtreleme ve arama
        Task<IDataResult<List<FreeBarberGetDto>>> GetFilteredFreeBarbersAsync(FilterRequestDto filter);
        
        Task<IDataResult<FreeBarberMinePanelDto>> GetMyPanel(Guid currentUserId);
        Task<IDataResult<FreeBarberMinePanelDetailDto>> GetMyPanelDetail(Guid panelId);
        Task<IDataResult<FreeBarberMinePanelDto>> GetFreeBarberForUsers(Guid freeBarberId);
        Task<IResult> UpdateLocationAsync(UpdateLocationDto dto, Guid currentUserId);


        Task<IResult> UpdateAvailabilityAsync(bool isAvailable, Guid currentUserId);
    }
}
