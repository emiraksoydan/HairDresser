using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using Core.Utilities.Security.Hashing;
using Core.Utilities.Security.JWT;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.Extensions.Configuration;
using Core.Aspect.Autofac.Transaction;

namespace Business.Concrete
{
    public class UserManager(IUserDal userDal, IPhoneService phoneService, ITokenHelper tokenHelper, IImageService imageService, IRefreshTokenService refreshTokenService, IRefreshTokenDal refreshTokenDal, IConfiguration configuration) : IUserService
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

        public async Task<IResult> Update(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);
            return new SuccessResult("Kullanıcı güncellendi");
        }

        public async Task<IDataResult<UserProfileDto>> GetMe(Guid userId)
        {
            var user = await userDal.Get(u => u.Id == userId);
            if (user == null)
                return new ErrorDataResult<UserProfileDto>("Kullanıcı bulunamadı");

            string phone = string.Empty;
            phone = phoneService.Decrypt(user.PhoneEncrypted, user.PhoneEncryptedNonce);
   

            // Get user image if exists
            ImageGetDto imageDto = null;
            if (user.ImageId.HasValue)
            {
                var imageResult = await imageService.GetImage(user.ImageId.Value);
                if (imageResult.Success && imageResult.Data != null)
                {
                    imageDto = imageResult.Data;
                }
            }

            var userProfile = new UserProfileDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = phone,
                UserType = user.UserType,
                ImageId = user.ImageId,
                Image = imageDto,
                IsActive = user.IsActive
            };

            return new SuccessDataResult<UserProfileDto>(userProfile, "Kullanıcı bilgileri getirildi");
        }

        [ValidationAspect(typeof(UpdateUserDtoValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> UpdateProfile(UpdateUserDto dto, Guid currentUserId)
        {
            // Get current user
            var currentUserResult = await GetById(currentUserId);
            if (currentUserResult.Data == null)
            {
                return new ErrorDataResult<AccessToken>("Kullanıcı bulunamadı");
            }

            var currentUser = currentUserResult.Data;

            // Normalize phone number
            var e164 = phoneService.NormalizeToE164(dto.PhoneNumber);

            // Check if phone number is already in use by another user
            var phoneCheck = await GetByPhone(e164);
            if (phoneCheck.Data != null && phoneCheck.Data.Id != currentUserId)
            {
                return new ErrorDataResult<AccessToken>("Bu telefon numarası başka bir kullanıcı tarafından kullanılıyor");
            }

            // Update user fields
            currentUser.FirstName = dto.FirstName;
            currentUser.LastName = dto.LastName;

            // Update phone if changed
            if (phoneCheck.Data == null || phoneCheck.Data.Id == currentUserId)
            {
                var (cipher, nonce) = phoneService.Encrypt(e164);
                currentUser.PhoneEncrypted = cipher;
                currentUser.PhoneEncryptedNonce = nonce;
                currentUser.PhoneSearchToken = phoneService.ComputeSearchToken(e164);
            }

            // Update user
            var updateResult = await Update(currentUser);
            if (!updateResult.Success)
            {
                return new ErrorDataResult<AccessToken>(updateResult.Message);
            }

            // Revoke all active refresh tokens for this user (güvenlik için)
            var activeTokens = await refreshTokenDal.GetActiveByUser(currentUserId);
            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = null; // Could be passed from controller if needed
                await refreshTokenDal.Update(token);
            }

            // Generate new access token with updated claims
            var claims = await GetClaims(currentUser);
            var newAccessToken = tokenHelper.CreateToken(currentUser, claims.Data);

            // Create new refresh token (like in AuthManager)
            var refreshDays = configuration.GetSection("TokenOptions:RefreshTokenExpirationDays").Get<int?>() ?? 30;
            var rt = refreshTokenService.CreateNew(refreshDays);
            var familyId = Guid.NewGuid();

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = currentUser.Id,
                TokenHash = rt.Hash,
                TokenSalt = rt.Salt,
                Fingerprint = rt.Fingerprint,
                FamilyId = familyId,
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = null, // Could be passed from controller if needed
                Device = null, // Could be passed from controller if needed
                ExpiresAt = rt.Expires
            };

            await refreshTokenDal.Add(refreshTokenEntity);

            return new SuccessDataResult<AccessToken>(new AccessToken
            {
                Token = newAccessToken.Token,
                Expiration = newAccessToken.Expiration,
                RefreshToken = rt.Plain,
                RefreshTokenExpires = rt.Expires
            }, "Profil başarıyla güncellendi");
        }
    }
}
