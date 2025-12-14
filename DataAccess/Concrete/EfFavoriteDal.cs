using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfFavoriteDal : EfEntityRepositoryBase<Favorite, DatabaseContext>, IFavoriteDal
    {
        private readonly DatabaseContext _context;

        public EfFavoriteDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Favorite> GetByUsersAsync(Guid favoritedFromId, Guid favoritedToId)
        {
            // Aktif veya pasif fark etmeksizin favoriyi getir (toggle için)
            return await _context.Favorites
                .FirstOrDefaultAsync(f => 
                    f.FavoritedFromId == favoritedFromId && 
                    f.FavoritedToId == favoritedToId);
        }

        public async Task<bool> ExistsAsync(Guid favoritedFromId, Guid favoritedToId)
        {
            // Sadece aktif favorileri kontrol et
            return await _context.Favorites
                .AnyAsync(f => 
                    f.FavoritedFromId == favoritedFromId && 
                    f.FavoritedToId == favoritedToId &&
                    f.IsActive);
        }
    }
}
