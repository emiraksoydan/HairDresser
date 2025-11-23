using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Core.DataAccess
{
    public interface IEntityRepository<T> where T : class,IEntity
    {
        Task<List<T>> GetAll(Expression<Func<T,bool>> filter = null);
        Task<T> Get(Expression<Func<T, bool>> filter);
        Task Add(T entity);
        Task AddRange(List<T> entities);
        Task UpdateRange(List<T> entities);
        Task Update(T entity);
        Task Remove(T entity);
        Task DeleteAll(Expression<Func<T, bool>> filter);
        Task<bool> AnyAsync(Expression<Func<T, bool>> filter);

    }
}
