using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfFreeBarberDal : EfEntityRepositoryBase<FreeBarber, DatabaseContext>, IFreeBarberDal
    {
        private readonly DatabaseContext _context;
        public EfFreeBarberDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

   
    }
}
