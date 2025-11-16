using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface IAuthService
    {

        Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> VerifyOtpAsync(UserForVerifyDto userForVerifyDto, string? ip, string? device);
        Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> RefreshAsync(string refreshToken, string? ip);
        Task<IResult> SendOtpAsync(string phoneNumber, UserType? userType,OtpPurpose otpPurpose);
        Task<IResult> RevokeAsync(Guid userId, string refreshToken, string? ip);

        Task<IResult> LoginWithPassword(UserForVerifyDto userForVerifyDto, string? ip, string? device);
    }
}
