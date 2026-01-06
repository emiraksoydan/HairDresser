using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IUserFcmTokenDal : IEntityRepository<UserFcmToken>
    {
        Task<List<UserFcmToken>> GetActiveTokensByUserIdAsync(Guid userId);
        Task<UserFcmToken?> GetByTokenAsync(string fcmToken);
        Task DeactivateTokenAsync(string fcmToken);
        Task DeactivateAllUserTokensAsync(Guid userId);
    }
}

