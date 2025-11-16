using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfNotificationDal : EfEntityRepositoryBase<Notification, DatabaseContext>, INotificationDal
    {
        private readonly DatabaseContext _context;
        public EfNotificationDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }
     
    }
}
