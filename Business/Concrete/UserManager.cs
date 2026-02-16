
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.Extensions.Configuration;
using Core.Aspect.Autofac.Transaction;

namespace Business.Concrete
{
    public class UserManager(
        IUserDal userDal,
        IPhoneService phoneService,
        ITokenHelper tokenHelper,
        IImageService imageService,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenDal refreshTokenDal,
        IConfiguration configuration,
        IOperationClaimDal operationClaimDal,
        IUserOperationClaimService userOperationClaimService,
        ISmsVerifyService smsVerifyService) : IUserService
    {
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> Add(User user)
        {
            await userDal.Add(user);
            
            // Kullanıcıya UserType'a göre rol ata
            await AssignRoleToUserAsync(user);
            
            return new SuccessResult("Kullanıcı Eklendi");
        }
        
        private async Task AssignRoleToUserAsync(User user)
        {
            var rolesToAssign = new List<string>();
            
            // UserType'a göre spesifik rol ver
            // Not: "User" rolü kaldırıldı - her kullanıcı zaten Customer, FreeBarber veya BarberStore rolüne sahip
            switch (user.UserType)
            {
                case UserType.Customer:
                    rolesToAssign.Add("Customer");
                    break;
                case UserType.FreeBarber:
                    rolesToAssign.Add("FreeBarber");
                    break;
                case UserType.BarberStore:
                    rolesToAssign.Add("BarberStore");
                    break;
            }
            
            // Kullanıcının mevcut rollerini kontrol et
            var existingClaimsResult = await userOperationClaimService.GetClaimByUserId(user.Id);
            var existingClaimIds = new HashSet<Guid>();
            
            if (existingClaimsResult.Success && existingClaimsResult.Data != null)
            {
                existingClaimIds = existingClaimsResult.Data.Select(uoc => uoc.OperationClaimId).ToHashSet();
            }
            
            // Rolleri veritabanından bul ve ata
            var userOperationClaims = new List<UserOperationClaim>();
            
            foreach (var roleName in rolesToAssign)
            {
                // Rolü veritabanından bul veya oluştur
                var operationClaim = await operationClaimDal.Get(oc => oc.Name == roleName);
                
                if (operationClaim == null)
                {
                    // Rol veritabanında yoksa oluştur
                    operationClaim = new OperationClaim { Name = roleName };
                    await operationClaimDal.Add(operationClaim);
                }
                
                if (operationClaim != null && !existingClaimIds.Contains(operationClaim.Id))
                {
                    userOperationClaims.Add(new UserOperationClaim
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        OperationClaimId = operationClaim.Id
                    });
                }
            }
            
            // Rolleri ata (varsa)
            if (userOperationClaims.Any())
            {
                await userOperationClaimService.AddUserOperationsClaim(userOperationClaims);
            }
        }
        public async Task<IDataResult<User>> GetByPhone(string phoneNumber)
        {
            var e164 = phoneService.NormalizeToE164(phoneNumber);
            var user = await userDal.Get(u => u.PhoneNumber == e164);
            return new SuccessDataResult<User>(user);
        }

        public async Task<IDataResult<List<User>>> GetByPhoneAll(string phoneNumber)
        {
            var users = await userDal.GetByPhoneAll(phoneNumber);
            return new SuccessDataResult<List<User>>(users);
        }

        public async Task<IDataResult<User>> GetByCustomerNumber(string customerNumber)
        {
            var user = await userDal.GetByCustomerNumber(customerNumber);
            return new SuccessDataResult<User>(user);
        }

        public async Task<IDataResult<List<User>>> GetByCustomerNumberAll(string customerNumber)
        {
            var users = await userDal.GetByCustomerNumberAll(customerNumber);
            return new SuccessDataResult<List<User>>(users);
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

        [LogAspect]
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

            // Telefon numarasını direkt string olarak döndür
            string phone = user.PhoneNumber ?? string.Empty; // PhoneNumber required olduğu için genelde null olmaz ama güvenlik için

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
                CustomerNumber = user.CustomerNumber,
                ImageId = user.ImageId,
                Image = imageDto,
                IsActive = user.IsActive,
                IsKvkkApproved = user.IsKvkkApproved
            };

            return new SuccessDataResult<UserProfileDto>(userProfile, "Kullanıcı bilgileri getirildi");
        }

        [LogAspect]
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

            // Telefon alanı bu endpoint'ten güncellenmez — OTP doğrulamalı UpdatePhoneAsync kullanılmalı
            // Update user fields
            currentUser.FirstName = dto.FirstName;
            currentUser.LastName = dto.LastName;

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

        [LogAspect(logParameters: false)]
        public async Task<IResult> SendPhoneChangeOtpAsync(Guid currentUserId, string newPhone)
        {
            var e164 = phoneService.NormalizeToE164(newPhone);
            if (string.IsNullOrWhiteSpace(e164))
                return new ErrorResult("Geçersiz telefon numarası.");

            var currentUserResult = await GetById(currentUserId);
            if (currentUserResult.Data == null)
                return new ErrorResult("Kullanıcı bulunamadı.");

            if (currentUserResult.Data.PhoneNumber == e164)
                return new ErrorResult("Girilen numara mevcut numaranızla aynı.");

            // Yeni numara başka kullanıcıya ait mi?
            var existing = await GetByPhone(e164);
            if (existing.Data != null && existing.Data.Id != currentUserId)
                return new ErrorResult("Bu telefon numarası başka bir kullanıcı tarafından kullanılıyor.");

            return await smsVerifyService.SendAsync(e164);
        }

        [LogAspect(logParameters: false)]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> UpdatePhoneAsync(Guid currentUserId, string newPhone, string otpCode)
        {
            var e164 = phoneService.NormalizeToE164(newPhone);

            // OTP doğrula
            var verifyResult = await smsVerifyService.CheckAsync(e164, otpCode);
            if (!verifyResult.Success)
                return new ErrorDataResult<AccessToken>(verifyResult.Message);

            var currentUserResult = await GetById(currentUserId);
            if (currentUserResult.Data == null)
                return new ErrorDataResult<AccessToken>("Kullanıcı bulunamadı.");

            var currentUser = currentUserResult.Data;

            // Aynı müşteri numarasına sahip tüm kullanıcıların telefonunu güncelle
            if (!string.IsNullOrEmpty(currentUser.CustomerNumber))
            {
                var siblings = await GetByCustomerNumberAll(currentUser.CustomerNumber);
                if (siblings.Data != null)
                {
                    foreach (var sibling in siblings.Data)
                    {
                        sibling.PhoneNumber = e164;
                        sibling.UpdatedAt = DateTime.UtcNow;
                        await userDal.Update(sibling);
                    }
                }
            }
            else
            {
                currentUser.PhoneNumber = e164;
                currentUser.UpdatedAt = DateTime.UtcNow;
                await userDal.Update(currentUser);
            }

            // Güvenlik: mevcut refresh token'ları iptal et
            var activeTokens = await refreshTokenDal.GetActiveByUser(currentUserId);
            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                await refreshTokenDal.Update(token);
            }

            // Yeni token üret
            var claims = await GetClaims(currentUser);
            var newAccessToken = tokenHelper.CreateToken(currentUser, claims.Data);
            var refreshDays = configuration.GetSection("TokenOptions:RefreshTokenExpirationDays").Get<int?>() ?? 30;
            var rt = refreshTokenService.CreateNew(refreshDays);
            await refreshTokenDal.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = currentUser.Id,
                TokenHash = rt.Hash,
                TokenSalt = rt.Salt,
                Fingerprint = rt.Fingerprint,
                FamilyId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = rt.Expires
            });

            return new SuccessDataResult<AccessToken>(new AccessToken
            {
                Token = newAccessToken.Token,
                Expiration = newAccessToken.Expiration,
                RefreshToken = rt.Plain,
                RefreshTokenExpires = rt.Expires
            }, "Telefon numarası başarıyla güncellendi.");
        }
    }
}
