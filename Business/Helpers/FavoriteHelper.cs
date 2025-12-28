using DataAccess.Abstract;

namespace Business.Helpers
{
    public class FavoriteHelper
    {
        private readonly IFavoriteDal _favoriteDal;

        public FavoriteHelper(IFavoriteDal favoriteDal)
        {
            _favoriteDal = favoriteDal;
        }

        public async Task<bool> IsFavoriteActiveAsync(Guid userId1, Guid userId2, Guid? storeId = null)
        {
            var conditions = new List<(Guid from, Guid to)>
            {
                (userId1, userId2),
                (userId2, userId1)
            };

            if (storeId.HasValue)
            {
                conditions.Add((userId1, storeId.Value));
                conditions.Add((userId2, storeId.Value));
            }

            var favorites = await _favoriteDal.GetAll(f =>
                conditions.Any(c => c.from == f.FavoritedFromId && c.to == f.FavoritedToId) &&
                f.IsActive
            );

            return favorites.Count > 0;
        }

        public async Task<bool> IsFavoriteActiveMultipleParticipantsAsync(
            Guid currentUserId,
            List<Guid> participantIds,
            Guid? storeId = null)
        {
            var allUserIds = new List<Guid>(participantIds) { currentUserId };

            var favorites = await _favoriteDal.GetAll(f =>
                allUserIds.Contains(f.FavoritedFromId) &&
                allUserIds.Contains(f.FavoritedToId) &&
                f.IsActive
            );

            if (favorites.Count > 0)
                return true;

            if (storeId.HasValue)
            {
                var storeFavorites = await _favoriteDal.GetAll(f =>
                    allUserIds.Contains(f.FavoritedFromId) &&
                    f.FavoritedToId == storeId.Value &&
                    f.IsActive
                );

                return storeFavorites.Count > 0;
            }

            return false;
        }

        public async Task<Dictionary<Guid, bool>> GetBulkFavoriteStatusAsync(
            Guid currentUserId,
            List<Guid> targetIds)
        {
            var favorites = await _favoriteDal.GetAll(f =>
                f.FavoritedFromId == currentUserId &&
                targetIds.Contains(f.FavoritedToId) &&
                f.IsActive
            );

            var favoriteSet = new HashSet<Guid>(favorites.Select(f => f.FavoritedToId));

            return targetIds.ToDictionary(
                id => id,
                id => favoriteSet.Contains(id)
            );
        }
    }
}
