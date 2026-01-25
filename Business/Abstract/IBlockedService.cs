using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IBlockedService
    {
        Task<IDataResult<BlockedGetDto>> BlockUserAsync(Guid userId, CreateBlockedDto dto);
        Task<IDataResult<bool>> UnblockUserAsync(Guid userId, Guid blockedToUserId);
        Task<IDataResult<List<BlockedGetDto>>> GetMyBlockedUsersAsync(Guid userId);
        Task<IDataResult<BlockStatusDto>> GetBlockStatusAsync(Guid userId, Guid otherUserId);
        Task<IDataResult<HashSet<Guid>>> GetAllBlockedUserIdsAsync(Guid userId);
    }
}
