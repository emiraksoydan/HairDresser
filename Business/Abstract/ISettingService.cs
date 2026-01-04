using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface ISettingService
    {
        Task<IDataResult<SettingGetDto>> GetByUserIdAsync(Guid userId);
        Task<IResult> UpdateAsync(Guid userId, SettingUpdateDto dto);
        Task<IResult> InitializeDefaultAsync(Guid userId);
    }
}

