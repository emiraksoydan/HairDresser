using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IHelpGuideService
    {
        Task<IDataResult<List<HelpGuideGetDto>>> GetByUserTypeAsync(int userType);
        Task<IDataResult<List<HelpGuideGetDto>>> GetActiveByUserTypeAsync(int userType);
    }
}
