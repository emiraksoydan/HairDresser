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
    public class EfChatThreadDal : EfEntityRepositoryBase<ChatThread, DatabaseContext>, IChatThreadDal
    {
        public EfChatThreadDal(DatabaseContext context) : base(context) { }
    }
}
