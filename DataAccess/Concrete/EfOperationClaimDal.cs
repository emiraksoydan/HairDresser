using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfOperationClaimDal : EfEntityRepositoryBase<OperationClaim, DatabaseContext>, IOperationClaimDal
    {
        public EfOperationClaimDal(DatabaseContext context) : base(context)
        {
        }
    }
}
