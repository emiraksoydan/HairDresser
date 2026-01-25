using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IRequestService
    {
        Task<IDataResult<RequestGetDto>> CreateRequestAsync(Guid userId, CreateRequestDto dto);
        Task<IDataResult<List<RequestGetDto>>> GetMyRequestsAsync(Guid userId);
        Task<IDataResult<bool>> DeleteRequestAsync(Guid userId, Guid requestId);
    }
}
