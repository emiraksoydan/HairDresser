
using Business.Abstract;
using Business.ValidationRules.FluentValidation;
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
using System.Diagnostics;


namespace Business.Concrete
{
    public class AuthManager(
        IUserService userService, 
        ITokenHelper tokenHelper, 
        IPhoneService phoneService, 
        ITwilioVerifyService twilioVerify,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenDal refreshTokenDal, 
        IConfiguration configuration) : IAuthService
    {

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
            var send = await twilioVerify.SendAsync(e164);
            return send.Success ? send : new ErrorResult(send.Message);
        }

        [ValidationAspect(typeof(VerifyOtpValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> VerifyOtpAsync(UserForVerifyDto userForVerifyDto, string? ip, string? device)
        {
            var e164 = phoneService.NormalizeToE164(userForVerifyDto.PhoneNumber);
            var ok = await twilioVerify.CheckAsync(e164, userForVerifyDto.Code);
            if (!ok.Success) return new ErrorDataResult<AccessToken>(ok.Message);
            var existing = await userService.GetByPhone(e164);
            User user;
            if (existing.Data is not null) user = existing.Data;
            else
            {
                var (cipher, nonce) = phoneService.Encrypt(e164);
                user = new User
                {

                    FirstName = userForVerifyDto.FirstName,
                    LastName = userForVerifyDto.LastName,
                    UserType = userForVerifyDto.UserType,
                    PhoneEncrypted = cipher,
                    PhoneEncryptedNonce = nonce,
                    PhoneSearchToken = phoneService.ComputeSearchToken(e164),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                await userService.Add(user);
            }
            return await CreateAccessAndRefreshAsync(user, ip, device, familyId: null);

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

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<AccessToken>> LoginWithPassword(UserForVerifyDto userForVerifyDto, string? ip, string? device)
        {
            // 1. Telefon numarasını normalize et ve sisteme uygun hale getir
            var e164 = phoneService.NormalizeToE164(userForVerifyDto.PhoneNumber);

            // 2. İsim bilgilerini hazırla
            var firstName = userForVerifyDto.FirstName ?? string.Empty;
            var lastName = userForVerifyDto.LastName ?? string.Empty;

            // 3. Telefon numarasına göre kullanıcıyı ara (Unique check için kritik)
            var existing = await userService.GetByPhone(e164);

            // Mode kontrolü
            var isRegister = string.Equals(userForVerifyDto.Mode, "register", StringComparison.OrdinalIgnoreCase);

            User user;

            if (isRegister)
            {
                // --- KAYIT MODU ---
                if (existing.Data is not null)
                {
                    // Kullanıcı bu telefonla zaten kayıtlıysa hata dön
                    return new ErrorDataResult<AccessToken>("Bu telefon numarası zaten sisteme kayıtlı. Lütfen giriş yapın.");
                }

                // Yeni kullanıcı tipi geçerli mi kontrol et
                if (userForVerifyDto.UserType < UserType.Customer || userForVerifyDto.UserType > UserType.BarberStore)
                {
                    return new ErrorDataResult<AccessToken>("Geçersiz kullanıcı tipi.");
                }

                // Yeni kullanıcı oluşturma süreci
                var (cipher, nonce) = phoneService.Encrypt(e164);
                user = new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    UserType = userForVerifyDto.UserType,
                    PhoneEncrypted = cipher,
                    PhoneEncryptedNonce = nonce,
                    PhoneSearchToken = phoneService.ComputeSearchToken(e164),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                // Kullanıcıyı kaydet
                var addResult = await userService.Add(user);
                if (!addResult.Success) return new ErrorDataResult<AccessToken>(addResult.Message);

               
            }
            else
            {
                // --- GİRİŞ MODU ---
                if (existing.Data is null)
                {
                    // Telefon numarası sistemde yoksa giriş yapılamaz
                    return new ErrorDataResult<AccessToken>("Kullanıcı bulunamadı. Lütfen önce kayıt olun.");
                }

                user = existing.Data;
            }

            // Access ve Refresh Token üretimi
            return await CreateAccessAndRefreshAsync(user, ip, device, familyId: null);
        }


        private async Task<IDataResult<AccessToken>> CreateAccessAndRefreshAsync(User user, string? ip, string? device, Guid? familyId)
        {
            // Debug: Token oluşturulmadan önce user bilgilerini logla
            Debug.WriteLine($"[AuthManager] CreateAccessAndRefreshAsync: Creating token for UserId: {user.Id}, UserType: {user.UserType} (ToString: {user.UserType.ToString()})");
            
            var claims = await userService.GetClaims(user);
            var access = tokenHelper.CreateToken(user, claims.Data);
            
            // Debug: Token oluşturulduktan sonra logla
            Debug.WriteLine($"[AuthManager] CreateAccessAndRefreshAsync: Token created successfully for UserId: {user.Id}, UserType: {user.UserType}");
            
            // Get refresh token expiration from configuration (default: 30 days)
            var refreshDays = configuration.GetSection("TokenOptions:RefreshTokenExpirationDays").Get<int?>() ?? 30;
            var rt = refreshTokenService.CreateNew(refreshDays);
            var fam = familyId ?? Guid.NewGuid();
            var entity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
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


    }
}
