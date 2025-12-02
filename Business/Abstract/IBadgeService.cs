using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IBadgeService
    {
        Task<IDataResult<BadgeCountDto>> GetCountsAsync(Guid userId);
    }
}
