using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IRefreshTokenDal : IEntityRepository<RefreshToken> 
    {
        Task Add(RefreshToken token);
        Task Update(RefreshToken token);
        Task<RefreshToken?> GetByFingerprintAsync(string fingerprint);
        Task<List<RefreshToken>> GetActiveByUser(Guid userId);
        Task RevokeFamilyAsync(Guid familyId, string reason, string? ip);
    }
}
