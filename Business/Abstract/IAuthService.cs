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

namespace Business.Abstract
{
    public interface IAuthService
    {
        Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> Register(UserForRegisterDto userForRegisterDto, string password);
        Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> Login(UserForLoginDto dto);
        Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> Refresh(string refreshToken);
        Task<IResult> UserExists(string email);
        Task<IResult> CheckIdendityNumber(string identityNumber);
        //Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> CreateAccessToken(User user);
        Task<IResult> Logout(string refreshToken);
    }
}
