using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IUserDal : IEntityRepository<User>
    {
        Task<List<OperationClaim>> GetClaims(User user);
        Task<List<User>> GetByPhoneAll(string phoneNumber); // Aynı telefon numarasına sahip tüm kullanıcıları getir
        Task<User> GetByCustomerNumber(string customerNumber); // Müşteri numarasına göre kullanıcı getir
        Task<List<User>> GetByCustomerNumberAll(string customerNumber); // Aynı müşteri numarasına sahip tüm kullanıcıları getir
    }
}
