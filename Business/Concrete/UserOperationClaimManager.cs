using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class UserOperationClaimManager(IUserOperationClaimDal userOperationClaimDal) : IUserOperationClaimService
    {
        public async Task<IDataResult<List<UserOperationClaim>>> AddUserOperationsClaim(List<UserOperationClaim> userOperationClaims)
        {
            await userOperationClaimDal.AddRange(userOperationClaims);
            return new SuccessDataResult<List<UserOperationClaim>>(Messages.UserOperationClaimsAdded);
        }

        public async Task<IDataResult<List<UserOperationClaim>>> GetClaimByUserId(Guid userId)
        {
            var userOperationsclaims = await userOperationClaimDal.GetAll(u=>u.UserId == userId);
            if (userOperationsclaims != null)
                return new SuccessDataResult<List<UserOperationClaim>>(userOperationsclaims);
            return new ErrorDataResult<List<UserOperationClaim>>(null!, Messages.UserOperationClaimsNotFound);
        }
    }
}
