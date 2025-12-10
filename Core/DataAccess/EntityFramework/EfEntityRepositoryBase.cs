using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Core.DataAccess.EntityFramework
{
    public class EfEntityRepositoryBase<TEntity, TContext>(TContext context) : IEntityRepository<TEntity> where TEntity : class, IEntity where TContext : DbContext
    {
        protected TContext Context => context;

        /// <summary>
        /// Adds entity to context. SaveChangesAsync is called automatically when TransactionScope completes.
        /// TransactionScopeAspect manages the transaction, and EF Core automatically saves changes when scope completes.
        /// </summary>
        public async Task Add(TEntity entity)
        {
            await context.Set<TEntity>().AddAsync(entity);
            // SaveChangesAsync will be called automatically by EF Core when TransactionScope completes
            // TransactionScopeAspect ensures all changes are saved atomically
        }

        /// <summary>
        /// Adds entities to context. SaveChangesAsync is called automatically when TransactionScope completes.
        /// </summary>
        public async Task AddRange(List<TEntity> entities)
        {
            await context.Set<TEntity>().AddRangeAsync(entities);
            // SaveChangesAsync will be called automatically by EF Core when TransactionScope completes
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
        /// Removes entity from context. SaveChangesAsync is called automatically when TransactionScope completes.
        /// </summary>
        public async Task Remove(TEntity entity)
        {
            context.Set<TEntity>().Remove(entity);
            // SaveChangesAsync will be called automatically by EF Core when TransactionScope completes
            await Task.CompletedTask;
        }

        /// <summary>
        /// Updates entity in context. SaveChangesAsync is called automatically when TransactionScope completes.
        /// </summary>
        public async Task Update(TEntity entity)
        {
            context.Set<TEntity>().Update(entity);
            // SaveChangesAsync will be called automatically by EF Core when TransactionScope completes
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Updates entities in context. SaveChangesAsync is called automatically when TransactionScope completes.
        /// </summary>
        public async Task UpdateRange(List<TEntity> entities)
        {
            context.Set<TEntity>().UpdateRange(entities);
            // SaveChangesAsync will be called automatically by EF Core when TransactionScope completes
            await Task.CompletedTask;
        }

        /// <summary>
        /// Removes entities from context. SaveChangesAsync is called automatically when TransactionScope completes.
        /// </summary>
        public async Task DeleteAll(List<TEntity> entities)
        {
            context.Set<TEntity>().RemoveRange(entities);
            // SaveChangesAsync will be called automatically by EF Core when TransactionScope completes
            await Task.CompletedTask;
        }
    }
}
