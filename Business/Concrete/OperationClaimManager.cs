using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class OperationClaimManager(IOperationClaimDal operationClaimDal) : IOperationClaimService
    {
        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<OperationClaim>>> GetAllOperationClaim()
        {
            var claims = await operationClaimDal.GetAll();
            return new SuccessDataResult<List<OperationClaim>>(claims);
        }
    }
}
