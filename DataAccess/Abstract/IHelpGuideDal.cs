using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IHelpGuideDal : IEntityRepository<HelpGuide>
    {
        Task<List<HelpGuide>> GetByUserTypeAsync(int userType);
        Task<List<HelpGuide>> GetActiveByUserTypeAsync(int userType);
    }
}
