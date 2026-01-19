
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
        IUserOperationClaimService userOperationClaimService) : IUserService
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
                IsActive = user.IsActive
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

            // Normalize phone number
            var e164 = phoneService.NormalizeToE164(dto.PhoneNumber);

            // Mevcut kullanıcının telefon numarası değişiyor mu kontrol et
            string currentPhone = currentUser.PhoneNumber ?? string.Empty; // PhoneNumber required olduğu için genelde null olmaz ama güvenlik için
            var isPhoneChanging = currentPhone != e164;

            if (isPhoneChanging)
            {
                // Aynı telefon numarasına sahip tüm kullanıcıları kontrol et
                var usersWithSamePhone = await GetByPhoneAll(e164);
                
                if (usersWithSamePhone.Data != null && usersWithSamePhone.Data.Any())
                {
                    // Aynı telefon numarasına sahip başka kullanıcı var mı kontrol et (kendisi hariç)
                    var otherUser = usersWithSamePhone.Data.FirstOrDefault(u => u.Id != currentUserId);
                    
                    if (otherUser != null)
                    {
                        // Eğer müşteri numaraları aynıysa ve userType farklıysa, telefon numarasını güncelle (hata verme)
                        if (otherUser.CustomerNumber == currentUser.CustomerNumber && otherUser.UserType != currentUser.UserType)
                        {
                            // Aynı müşteri numarasına sahip farklı tür kullanıcı - telefon numarasını güncelle
                            currentUser.PhoneNumber = e164;
                        }
                        else if (otherUser.CustomerNumber != currentUser.CustomerNumber)
                        {
                            // Farklı müşteri numarasına sahip kullanıcı - bu numara başka bir kullanıcıya ait
                            return new ErrorDataResult<AccessToken>("Bu telefon numarası başka bir kullanıcı tarafından kullanılıyor");
                        }
                        else if (otherUser.UserType == currentUser.UserType)
                        {
                            // Aynı userType - bu numara zaten bu kullanıcıya ait olmalı
                            return new ErrorDataResult<AccessToken>("Bu telefon numarası zaten sizin tarafınızdan kullanılıyor");
                        }
                    }
                    else
                    {
                        // Aynı telefon numarasına sahip başka kullanıcı yok - telefon numarasını güncelle
                        currentUser.PhoneNumber = e164;
                    }
                }
                else
                {
                    // Bu telefon numarası hiç kullanılmamış - telefon numarasını güncelle
                    currentUser.PhoneNumber = e164;
                }

                // Aynı müşteri numarasına sahip tüm kullanıcıların telefon numaralarını güncelle
                if (!string.IsNullOrEmpty(currentUser.CustomerNumber))
                {
                    var usersWithSameCustomerNumber = await GetByCustomerNumberAll(currentUser.CustomerNumber);
                    if (usersWithSameCustomerNumber.Data != null && usersWithSameCustomerNumber.Data.Any())
                    {
                        foreach (var user in usersWithSameCustomerNumber.Data)
                        {
                            if (user.Id != currentUserId && user.PhoneNumber != e164)
                            {
                                user.PhoneNumber = e164;
                                user.UpdatedAt = DateTime.UtcNow;
                                await userDal.Update(user);
                            }
                        }
                    }
                }
            }

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
    }
}
