using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using Core.Utilities.Security.Hashing;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class UserManager(IUserDal userDal) : IUserService
    {
        public async Task<IResult> Add(User user)
        {
            await userDal.Add(user);
            return new SuccessResult("Kullanıcı Eklendi");
        }

        public async Task<IDataResult<User>> GetByIdentityNumber(string identityNumber)
        {
            var users = await userDal.GetAll(t => t.IdentityNumberHash != null && t.IdentityNumberSalt != null);
            var user = users.FirstOrDefault(t =>
                HashingHelper.verifyValueHash(identityNumber, t.IdentityNumberHash, t.IdentityNumberSalt));

            return new SuccessDataResult<User>(user);
        }

        public async Task<IDataResult<User>> GetByMail(string email)
        {
            var user = await userDal.Get(u => u.Email == email);
            return new SuccessDataResult<User>(user);
        }


        public async Task<IDataResult<List<OperationClaim>>> GetClaims(User user)
        {
            var claims = await userDal.GetClaims(user);
            return new SuccessDataResult<List<OperationClaim>>(claims);
        }

        public async Task<IDataResult<User>> GetById(Guid id)
        {
            var user = await userDal.Get(u => u.Id == id);
            return new SuccessDataResult<User>(user);
        }
    }
}
