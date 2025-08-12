using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;

namespace DataAccess.Concrete
{
    public class EfWorkingHourDal : EfEntityRepositoryBase<WorkingHour,DatabaseContext>, IWorkingHourDal
    {
        public EfWorkingHourDal(DatabaseContext context) : base(context)
        {
        }
    }
   
}
