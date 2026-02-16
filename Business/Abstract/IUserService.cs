using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Abstract
{
    public interface IUserService
    {
        Task<IDataResult<List<OperationClaim>>> GetClaims(User user);
        Task<IResult> Add(User user);
        Task<IDataResult<User>> GetByPhone(string phoneNumber);
        Task<IDataResult<List<User>>> GetByPhoneAll(string phoneNumber); // Aynı telefon numarasına sahip tüm kullanıcıları getir
        Task<IDataResult<User>> GetByCustomerNumber(string customerNumber); // Müşteri numarasına göre kullanıcı getir
        Task<IDataResult<List<User>>> GetByCustomerNumberAll(string customerNumber); // Aynı müşteri numarasına sahip tüm kullanıcıları getir
        Task<IDataResult<User>> GetById(Guid id);
        Task<IDataResult<User>> GetByName(string firstName, string lastName);
        Task<IResult> Update(User user);
        Task<IDataResult<UserProfileDto>> GetMe(Guid userId);
        Task<IDataResult<AccessToken>> UpdateProfile(UpdateUserDto dto, Guid currentUserId);
        Task<IResult> SendPhoneChangeOtpAsync(Guid currentUserId, string newPhone);
        Task<IDataResult<AccessToken>> UpdatePhoneAsync(Guid currentUserId, string newPhone, string otpCode);
    }
}
