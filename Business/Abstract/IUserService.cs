using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Entities;

namespace Business.Abstract
{
    public interface IUserService
    {
        Task<IDataResult<List<OperationClaim>>> GetClaims(User user);
        Task<IResult> Add(User user);
        Task<IDataResult<User>> GetByPhone(string phoneNumber);
        Task<IDataResult<User>> GetById(Guid id);
        Task<IDataResult<User>> GetByName(string firstName, string lastName);


    }
}
