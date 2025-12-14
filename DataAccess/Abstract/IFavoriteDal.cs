using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IFavoriteDal : IEntityRepository<Favorite>
    {
        Task<Favorite> GetByUsersAsync(Guid favoritedFromId, Guid favoritedToId);
        Task<bool> ExistsAsync(Guid favoritedFromId, Guid favoritedToId);
    }
}
