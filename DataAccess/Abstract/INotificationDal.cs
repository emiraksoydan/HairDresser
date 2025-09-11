using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Core.Utilities.Results;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface INotificationDal : IEntityRepository<Notification>
    {
        Task<int> GetUnreadCountAsync(Guid userId);
    }
}
