using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IUserSummaryService
    {
        Task<IDataResult<UserNotifyDto?>> TryGetAsync(Guid userId);
        Task<IDataResult<Dictionary<Guid, UserNotifyDto>>> GetManyAsync(IEnumerable<Guid> userIds);
    }
}
