using Business.Abstract;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class UserOperationClaimManager(IUserOperationClaimDal userOperationClaimDal) : IUserOperationClaimService
    {
        [LogAspect]
        public async Task<IDataResult<List<UserOperationClaim>>> AddUserOperationsClaim(List<UserOperationClaim> userOperationClaims)
        {
            await userOperationClaimDal.AddRange(userOperationClaims);
            return new SuccessDataResult<List<UserOperationClaim>>(Messages.UserOperationClaimsAdded);
        }

        public async Task<IDataResult<List<UserOperationClaim>>> GetClaimByUserId(Guid userId)
        {
            var userOperationsClaims = await userOperationClaimDal.GetAll(u => u.UserId == userId);
            return new SuccessDataResult<List<UserOperationClaim>>(userOperationsClaims);
        }
    }
}
