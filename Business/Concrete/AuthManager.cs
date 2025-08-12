using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using Core.Utilities.Security.Hashing;
using Core.Utilities.Security.JWT;
using DataAccess.Abstract;
using Entities.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.Extensions.Configuration;

namespace Business.Concrete
{
    public class AuthManager(IUserService userService,ITokenHelper tokenHelper, IRefreshTokenDal refreshTokenDal,
    IConfiguration configuration) : IAuthService
    {
        private readonly TokenOption _opts = configuration.GetSection("TokenOptions").Get<TokenOption>();
        private async Task<Core.Utilities.Security.JWT.AccessToken> CreateTokenWithRefresh(User user)
        {
            var claims = (await userService.GetClaims(user)).Data;
            var access = tokenHelper.CreateToken(user, claims);
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            HashingHelper.CreateHash(raw, out var hash, out var salt);
            var entity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = hash,
                TokenSalt = salt,
                Expires = DateTime.UtcNow.AddDays(_opts.RefreshTokenExpiration)
            };
            await refreshTokenDal.Add(entity);
            access.RefreshToken = raw;     
            return access;
        }
        [ValidationAspect(typeof(UserLoginValidator))]
        public async Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> Login(UserForLoginDto userForLoginDto)
        {
            var userToCheck = await userService.GetByMail(userForLoginDto.Email);
            if (userToCheck.Data == null)
            {
                return new ErrorDataResult<Core.Utilities.Security.JWT.AccessToken>("Kullanıcı bulunamadı");
            }

            if (!HashingHelper.verifyValueHash(userForLoginDto.Password, userToCheck.Data.PasswordHash, userToCheck.Data.PasswordSalt))
            {
                return new ErrorDataResult<Core.Utilities.Security.JWT.AccessToken>("Şifre hatalı");
            }

            var token = await CreateTokenWithRefresh(userToCheck.Data);
            return new SuccessDataResult<Core.Utilities.Security.JWT.AccessToken>(token, "Giriş başarılı");
        }
        [ValidationAspect(typeof(UserRegisterValidator))]
        public async Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> Register(UserForRegisterDto userForRegisterDto, string password)
        {
            byte[] passwordHash, passwordSalt;
            byte[] identityNumberHash, identityNumberSalt;
            HashingHelper.CreateHash(password, out passwordHash, out passwordSalt);
            var tcCheck = await userService.GetByIdentityNumber(userForRegisterDto.IdentityNumber);
            if (tcCheck.Data is not null)
                return new ErrorDataResult<Core.Utilities.Security.JWT.AccessToken>("Bu TC kimlik numarasına sahip kullanıcı zaten kayıtlı.");

            HashingHelper.CreateHash(userForRegisterDto.IdentityNumber, out identityNumberHash, out identityNumberSalt);

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = userForRegisterDto.FirstName,
                LastName = userForRegisterDto.LastName,
                Email = userForRegisterDto.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                IdentityNumberHash = identityNumberHash,
                IdentityNumberSalt = identityNumberSalt,
                CertificateFilePath = userForRegisterDto.CertificateFilePath,
                TaxDocumentFilePath = userForRegisterDto.TaxDocumentFilePath,
                UserType = userForRegisterDto.UserType,
                Status = true
            };
            await userService.Add(user);
            var token = await CreateTokenWithRefresh(user);
            return new SuccessDataResult<Core.Utilities.Security.JWT.AccessToken>(token, "Kayıt başarılı");
        }

        public async Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> Refresh(string refreshToken)
        {
            var actives = await refreshTokenDal.GetAll(t =>t.Revoked == null && t.Expires > DateTime.UtcNow); 
            var entity = actives.FirstOrDefault(t =>
                HashingHelper.verifyValueHash(refreshToken, t.TokenHash, t.TokenSalt));
            if (entity == null)
                return new ErrorDataResult<Core.Utilities.Security.JWT.AccessToken>("Refresh token geçersiz");
            entity.Revoked = DateTime.UtcNow;
            await refreshTokenDal.Update(entity);
            var user = (await userService.GetById(entity.UserId)).Data;
            var token = await CreateTokenWithRefresh(user);
            return new SuccessDataResult<Core.Utilities.Security.JWT.AccessToken>(token, "Token yenilendi");
        }

        public async Task<IResult> UserExists(string email)
        {
            var result = await userService.GetByMail(email);
            if (result.Data != null)
            {
                return new ErrorResult("Bu emaile sahip kullanıcı kayıtlı");
            }

            return new SuccessResult();
        }
        public async Task<IResult> CheckIdendityNumber(string identityNumber)
        {
            var result = await userService.GetByIdentityNumber(identityNumber);
            if (result.Data != null)
            {
                return new ErrorResult("Bu kimlik numarasına  sahip kullanıcı kayıtlı");
            }
            return new SuccessResult();
        }

        //public async Task<IDataResult<Core.Utilities.Security.JWT.AccessToken>> CreateAccessToken(User user)
        //{
        //    var claims = await userService.GetClaims(user);
        //    var accessToken = tokenHelper.CreateToken(user, claims.Data);
        //    return new SuccessDataResult<Core.Utilities.Security.JWT.AccessToken>(accessToken, "Token oluşturuldu");
        //}

        public async Task<IResult> Logout(string refreshToken)
        {
            var now = DateTime.UtcNow;
            var candidates = await refreshTokenDal
                .GetAll(r => r.Revoked == null && r.Expires > now);
            var entity = candidates.FirstOrDefault(r =>
                HashingHelper.verifyValueHash(refreshToken, r.TokenHash, r.TokenSalt));
            if (entity is null)
                return new ErrorResult("Refresh token bulunamadı veya iptal edilmiş.");
            await refreshTokenDal.Revoke(entity.Id);
            return new SuccessResult("Çıkış yapıldı.");
        }
    }
}
