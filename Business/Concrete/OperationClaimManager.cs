using Business.Abstract;
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
    internal class OperationClaimManager(IOperationClaimDal operationClaimDal) : IOperationClaimService
    {
        public async Task<IDataResult<List<OperationClaim>>> GetAllOperationClaim()
        {
            var claims = await operationClaimDal.GetAll();
            if (claims != null) {
                return new SuccessDataResult<List<OperationClaim>>(claims);
            }
            return new ErrorDataResult<List<OperationClaim>>(null!,"Yetkiler getirilemedi");
        }
    }
}
