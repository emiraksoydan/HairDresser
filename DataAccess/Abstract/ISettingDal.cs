using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface ISettingDal : IEntityRepository<Setting>
    {
        Task<Setting?> GetByUserIdAsync(Guid userId);
    }
}

