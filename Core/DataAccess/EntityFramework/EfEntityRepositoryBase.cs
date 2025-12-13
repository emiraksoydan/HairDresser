using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Core.DataAccess.EntityFramework
{
    public class EfEntityRepositoryBase<TEntity, TContext>(TContext context) : IEntityRepository<TEntity> where TEntity : class, IEntity where TContext : DbContext
    {
        protected TContext Context => context;



        /// <summary>
        /// Adds entity to context. Call SaveChangesAsync to persist changes.
        /// </summary>
        public async Task Add(TEntity entity)
        {
            await context.Set<TEntity>().AddAsync(entity);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Adds entities to context. Call SaveChangesAsync to persist changes.
        /// </summary>
        public async Task AddRange(List<TEntity> entities)
        {
            await context.Set<TEntity>().AddRangeAsync(entities);
            await context.SaveChangesAsync();
        }

        public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter)
        {
            return await context.Set<TEntity>().AnyAsync(filter);
        }

        public async Task<int> CountAsync(Expression<Func<TEntity, bool>> filter)
        {
            return await context.Set<TEntity>().CountAsync(filter);
        }

        public async Task<TEntity> Get(Expression<Func<TEntity, bool>> filter)
        {
            return await context.Set<TEntity>().FirstOrDefaultAsync(filter);
        }

        public async Task<List<TEntity>> GetAll(Expression<Func<TEntity, bool>> filter = null)
        {
            return filter == null
                ? await context.Set<TEntity>().ToListAsync()
                : await context.Set<TEntity>().Where(filter).ToListAsync();
        }

        /// <summary>
        /// Removes entity from context. Call SaveChangesAsync to persist changes.
        /// </summary>
        public async Task Remove(TEntity entity)
        {
            context.Set<TEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Updates entity in context. Call SaveChangesAsync to persist changes.
        /// </summary>
        public async Task Update(TEntity entity)
        {
            context.Set<TEntity>().Update(entity);
            await context.SaveChangesAsync();
        }
        
        /// <summary>
        /// Updates entities in context. Call SaveChangesAsync to persist changes.
        /// </summary>
        public async Task UpdateRange(List<TEntity> entities)
        {
            context.Set<TEntity>().UpdateRange(entities);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Removes entities from context. Call SaveChangesAsync to persist changes.
        /// </summary>
        public async Task DeleteAll(List<TEntity> entities)
        {
            context.Set<TEntity>().RemoveRange(entities);
            await context.SaveChangesAsync();
        }
    }
}
