using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfUserDal : EfEntityRepositoryBase<User, DatabaseContext>, IUserDal
    {
        private readonly DatabaseContext _context;
        private readonly IPhoneService _phoneService;
        public EfUserDal(DatabaseContext context, IPhoneService phoneService) : base(context)
        {
            _context = context;
            _phoneService = phoneService;
        }
        public async Task<List<OperationClaim>> GetClaims(User user)
        {  
                var result = from operationClaim in _context.OperationClaims
                             join userOperationClaim in _context.UserOperationClaims
                                 on operationClaim.Id equals userOperationClaim.OperationClaimId
                             where userOperationClaim.UserId == user.Id
                             select new OperationClaim { Id = operationClaim.Id, Name = operationClaim.Name };
                return await result.ToListAsync();
        }

        public async Task<List<User>> GetByPhoneAll(string phoneNumber)
        {
            var e164 = _phoneService.NormalizeToE164(phoneNumber);
            return await _context.Users
                .Where(u => u.PhoneNumber == e164)
                .ToListAsync();
        }

        public async Task<User> GetByCustomerNumber(string customerNumber)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.CustomerNumber == customerNumber);
        }

        public async Task<List<User>> GetByCustomerNumberAll(string customerNumber)
        {
            return await _context.Users
                .Where(u => u.CustomerNumber == customerNumber)
                .ToListAsync();
        }
    }
}
