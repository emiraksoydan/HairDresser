using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IBarberStoreChairDal : IEntityRepository<BarberChair>
    {
        Task<List<BarberChair>> GetAllWithManualBarberAsync(Expression<Func<BarberChair, bool>> filter);

    }
}
