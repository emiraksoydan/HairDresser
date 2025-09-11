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
    public class EfAppointmentServiceOfferingDal : EfEntityRepositoryBase<AppointmentServiceOffering, DatabaseContext>, IAppointmentServiceOffering
    {
        public EfAppointmentServiceOfferingDal(DatabaseContext context) : base(context)
        {
        }
    }
}
