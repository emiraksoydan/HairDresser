using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfAppointmentDal : EfEntityRepositoryBase<Appointment, DatabaseContext>, IAppointmentDal
    {
        private readonly DatabaseContext _context;
        public EfAppointmentDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

     
    }
}
