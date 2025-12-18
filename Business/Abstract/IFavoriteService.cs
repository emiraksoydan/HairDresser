using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IFavoriteService
    {
        Task<IDataResult<ToggleFavoriteResponseDto>> ToggleFavoriteAsync(Guid userId, ToggleFavoriteDto dto);
        Task<IDataResult<bool>> IsFavoriteAsync(Guid userId, Guid targetId);
        Task<IDataResult<List<FavoriteGetDto>>> GetMyFavoritesAsync(Guid userId);
        Task<IDataResult<bool>> RemoveFavoriteAsync(Guid userId, Guid targetId);
    }
}
