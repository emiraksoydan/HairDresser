using Core.Utilities.Results;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IOperationClaimService 
    {
        Task<IDataResult<List<OperationClaim>>> GetAllOperationClaim();
    }
}
