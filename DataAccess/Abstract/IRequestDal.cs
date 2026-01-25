using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IRequestDal : IEntityRepository<Request>
    {
        Task<List<Request>> GetByUserAsync(Guid userId);
    }
}
