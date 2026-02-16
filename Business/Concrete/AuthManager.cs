
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Business;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.Extensions.Configuration;

namespace Business.Concrete
{
    public class AuthManager(
        IUserService userService, 
        ITokenHelper tokenHelper,
        IPhoneService phoneService,
        ISmsVerifyService smsVerify,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenDal refreshTokenDal, 
        IConfiguration configuration) : IAuthService
    {

        [LogAspect(logParameters: false)] // OTP kodu hassas veri olduğu için parametreleri loglamıyoruz
        [ValidationAspect(typeof(SendOtpValidator))]
        public async Task<IResult> SendOtpAsync(string phoneNumber, UserType? userType, OtpPurpose otpPurpose)
        {
            var e164 = phoneService.NormalizeToE164(phoneNumber);
            var existing = await userService.GetByPhone(e164);
            switch (otpPurpose)
            {
                case OtpPurpose.Register:
                    if (existing.Data is not null && existing.Data.UserType == userType)
                        return new ErrorResult("Bu telefon numarası zaten kayıtlı.");
                    break;
                case OtpPurpose.Login:
                    if (existing.Data is null)
                        return new ErrorResult("Kullanıcı bulunamadı.");
                    break;
                case OtpPurpose.Reset:
                    if (existing.Data is null)
                        return new ErrorResult("Bu numarayla kayıtlı kullanıcı bulunamadı.");
                    break;
            }
            var send = await smsVerify.SendAsync(e164);
            return send.Success ? send : new ErrorResult(send.Message);
        }

        [LogAspect(logParameters: false)] // OTP kodu hassas veri olduğu için parametreleri loglamıyoruz
        [ValidationAspect(typeof(VerifyOtpValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> VerifyOtpAsync(UserForVerifyDto userForVerifyDto, string? ip, string? device)
        {
            var e164 = phoneService.NormalizeToE164(userForVerifyDto.PhoneNumber);
            var ok = await smsVerify.CheckAsync(e164, userForVerifyDto.Code);
            if (!ok.Success) return new ErrorDataResult<AccessToken>(ok.Message);
            
            // Aynı telefon numarasına sahip tüm kullanıcıları kontrol et
            var usersWithSamePhone = await userService.GetByPhoneAll(e164);
            
            // Aynı telefon numarası ve aynı UserType ile kayıtlı kullanıcı var mı kontrol et
            if (usersWithSamePhone.Data != null && usersWithSamePhone.Data.Any())
            {
                var userWithSameType = usersWithSamePhone.Data.FirstOrDefault(u => u.UserType == userForVerifyDto.UserType);
                if (userWithSameType != null)
                {
                    // Aynı telefon numarası ve aynı UserType ile kayıtlı kullanıcı var - güncelle
                    var user = userWithSameType;
                    
                    // Mevcut kullanıcının telefon numarası eksikse veya farklıysa güncelle
                    if (string.IsNullOrWhiteSpace(user.PhoneNumber) || user.PhoneNumber != e164)
                    {
                        user.PhoneNumber = e164;
                        await userService.Update(user);
                    }
                    
                    return await CreateAccessAndRefreshAsync(user, ip, device, familyId: null);
                }
            }
            
            // Yeni kullanıcı oluştur
            string customerNumber = null;
            
            if (usersWithSamePhone.Data != null && usersWithSamePhone.Data.Any())
            {
                // Aynı telefon numarasına sahip kullanıcı varsa onun müşteri numarasını kullan
                customerNumber = usersWithSamePhone.Data.First().CustomerNumber;
            }
            else
            {
                // Yeni müşteri numarası oluştur (6 haneli rastgele sayı)
                customerNumber = await GenerateUniqueCustomerNumberAsync();
            }

            var newUser = new User
            {
                FirstName = userForVerifyDto.FirstName,
                LastName = userForVerifyDto.LastName,
                UserType = userForVerifyDto.UserType,
                PhoneNumber = e164, // E164 formatında telefon numarası
                CustomerNumber = customerNumber,
                IsActive = true,
                IsKvkkApproved = true,
                KvkkApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TrialEndDate = DateTime.UtcNow.AddMonths(2),
            };
            
            // Kullanıcıyı kaydet
            var addResult = await userService.Add(newUser);
            if (!addResult.Success)
            {
                return new ErrorDataResult<AccessToken>(addResult.Message);
            }
            
            // PhoneNumber'ın doğru kaydedildiğinden emin olmak için tekrar oku
            var savedUserResult = await userService.GetById(newUser.Id);
            if (savedUserResult.Data != null)
            {
                // PhoneNumber boşsa veya farklıysa güncelle
                if (string.IsNullOrWhiteSpace(savedUserResult.Data.PhoneNumber) || savedUserResult.Data.PhoneNumber != e164)
                {
                    savedUserResult.Data.PhoneNumber = e164;
                    await userService.Update(savedUserResult.Data);
                    newUser = savedUserResult.Data;
                }
                else
                {
                    newUser = savedUserResult.Data;
                }
            }
            
            return await CreateAccessAndRefreshAsync(newUser, ip, device, familyId: null);

        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> RefreshAsync(string plainRefresh, string? ip)
        {
            // 1) Fingerprint ile tek sorgu
            var fp = refreshTokenService.MakeFingerprint(plainRefresh);
            var token = await refreshTokenDal.GetByFingerprintAsync(fp);
            IResult rules = BusinessRules.Run(TokenNullControl(token), TokenVerifyConstTime(token, plainRefresh), ExpiryActive(token));
            if (rules != null)
                return (IDataResult<AccessToken>)rules;

            // 4) Reuse detection: daha önce devredilmiş/iptal edilmiş token tekrar kullanılıyorsa aileyi kapat
            if (token.ReplacedByFingerprint is not null)
            {
                await refreshTokenDal.RevokeFamilyAsync(token.FamilyId, "Reuse detected", ip);
                return new ErrorDataResult<AccessToken>("Güvenlik nedeniyle oturum kapatıldı.");
            }
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ip;
            var userRes = await userService.GetById(token.UserId);
            var user = userRes.Data;
            if (user is null)
                return new ErrorDataResult<AccessToken>("Hesap  bulunamadı.");

            var rotated = await CreateAccessAndRefreshAsync(user, ip, token.Device, familyId: token.FamilyId);
            var newFp = refreshTokenService.MakeFingerprint(rotated.Data.RefreshToken);
            token.ReplacedByFingerprint = newFp;
            await refreshTokenDal.Update(token);
            return rotated;
        }
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> RevokeAsync(Guid userId, string plainRefresh, string? ip)
        {
            var fp = refreshTokenService.MakeFingerprint(plainRefresh);
            var token = await refreshTokenDal.GetByFingerprintAsync(fp);
            if (token is null || token.UserId != userId)
                return new ErrorResult("Token bulunamadı.");

            if (!refreshTokenService.Verify(plainRefresh, token.TokenHash, token.TokenSalt))
                return new ErrorResult("Token bulunamadı.");

            if (token.RevokedAt is not null)
                return new ErrorResult("Token zaten iptal edilmiş.");

            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ip;
            await refreshTokenDal.Update(token);
            return new SuccessResult("Refresh token iptal edildi.");
        }



        private async Task<IDataResult<AccessToken>> CreateAccessAndRefreshAsync(User user, string? ip, string? device, Guid? familyId)
        {
            // Kullanıcıyı veritabanından yeniden yükle (role'lerin atandığından emin olmak için)
            var refreshedUser = await userService.GetById(user.Id);
            if (refreshedUser.Data == null)
            {
                return new ErrorDataResult<AccessToken>("Kullanıcı bulunamadı.");
            }
            
            var claims = await userService.GetClaims(refreshedUser.Data);
            var access = tokenHelper.CreateToken(refreshedUser.Data, claims.Data);
            
            // Get refresh token expiration from configuration (default: 30 days)
            var refreshDays = configuration.GetSection("TokenOptions:RefreshTokenExpirationDays").Get<int?>() ?? 30;
            var rt = refreshTokenService.CreateNew(refreshDays);
            var fam = familyId ?? Guid.NewGuid();
            var entity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = refreshedUser.Data.Id,
                TokenHash = rt.Hash,
                TokenSalt = rt.Salt,
                Fingerprint = rt.Fingerprint,
                FamilyId = fam,
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ip,
                Device = device,
                ExpiresAt = rt.Expires
            };

            await refreshTokenDal.Add(entity);

            return new SuccessDataResult<AccessToken>(new AccessToken
            {
                Token = access.Token,
                Expiration = access.Expiration,
                RefreshToken = rt.Plain,
                RefreshTokenExpires = rt.Expires
            }, "Giriş başarılı");
        }

        private IResult TokenNullControl(RefreshToken refreshToken)
        {
            if (refreshToken is null)
                return new ErrorDataResult<AccessToken>("Geçersiz refresh token.");
            return new SuccessDataResult<AccessToken>();
        }
        private IResult TokenVerifyConstTime(RefreshToken token, string plainRefresh)
        {
            if (token is null)
                return new ErrorDataResult<AccessToken>("Geçersiz refresh token.");

            if (!refreshTokenService.Verify(plainRefresh, token.TokenHash, token.TokenSalt))
                return new ErrorDataResult<AccessToken>("Geçersiz refresh token.");

            return new SuccessDataResult<AccessToken>();
        }
        private IResult ExpiryActive(RefreshToken token)
        {
            if (token is null)
                return new ErrorDataResult<AccessToken>("Geçersiz refresh token.");
            
            if (token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
                return new ErrorDataResult<AccessToken>("Süresi dolmuş veya iptal edilmiş token.");
            return new SuccessDataResult<AccessToken>();
        }

        /// <summary>
        /// Benzersiz müşteri numarası oluşturur (6 haneli rastgele sayı)
        /// </summary>
        private async Task<string> GenerateUniqueCustomerNumberAsync()
        {
            var random = new Random();
            string customerNumber;
            bool isUnique;
            int maxAttempts = 100; // Sonsuz döngüyü önlemek için
            int attempts = 0;

            do
            {
                // 6 haneli rastgele sayı oluştur (100000-999999)
                customerNumber = random.Next(100000, 999999).ToString();
                
                // Bu numaranın kullanılıp kullanılmadığını kontrol et
                var existingUser = await userService.GetByCustomerNumber(customerNumber);
                isUnique = existingUser.Data == null;
                attempts++;
            } while (!isUnique && attempts < maxAttempts);

            if (attempts >= maxAttempts)
            {
                throw new Exception("Müşteri numarası oluşturulamadı. Lütfen tekrar deneyin.");
            }

            return customerNumber;
        }


    }
}
