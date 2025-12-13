using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using Core.Utilities.Security.Hashing;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class UserManager(IUserDal userDal,IPhoneService phoneService) : IUserService
    {
        public async Task<IResult> Add(User user)
        {
            await userDal.Add(user);
            return new SuccessResult("Kullanıcı Eklendi");
        }
        public async Task<IDataResult<User>> GetByPhone(string phoneNumber)
        {
            var e164 = phoneService.NormalizeToE164(phoneNumber);
            var token = phoneService.ComputeSearchToken(e164);
            var user = await userDal.Get(u => u.PhoneSearchToken == token);
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

        public async Task<IDataResult<User>> GetByName(string firstName, string lastName)
        {
            var user = await userDal.Get(u => u.FirstName == firstName && u.LastName == lastName);
            return new SuccessDataResult<User>(user);
        }
    }
}
