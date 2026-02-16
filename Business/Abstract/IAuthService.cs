using System;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface IAuthService
    {
        Task<IResult> SendOtpAsync(string phoneNumber, UserType? userType, OtpPurpose otpPurpose);
        Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> VerifyOtpAsync(UserForVerifyDto userForVerifyDto, string? ip, string? device);
        Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> RefreshAsync(string refreshToken, string? ip);
        Task<IResult> RevokeAsync(Guid userId, string refreshToken, string? ip);
    }
}
