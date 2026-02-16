using Business.Abstract;
using Core.Aspect.Autofac.ExceptionHandling;
using Core.Utilities.Results;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Concrete
{
    /// <summary>
    /// Geçici olarak Twilio devre dışı - Default kod sistemi kullanılıyor
    /// Development için sabit kod: 123456
    /// Production'da Twilio'yu tekrar aktif etmek için bu dosyayı eski haline getirin
    /// </summary>
    public class TwilioVerifyManager : ITwilioVerifyService
    {
        // Development için default OTP kodu
        private const string DEFAULT_OTP_CODE = "123456";
        
        private readonly IConfiguration _configuration;
        private readonly bool _useTwilio;

        public TwilioVerifyManager(IConfiguration cfg)
        {
            _configuration = cfg;
            // appsettings.json'da "Twilio:Enabled" = false ise default kod kullanılır
            _useTwilio = cfg.GetValue<bool>("Twilio:Enabled", false);
            
            // Twilio kullanılıyorsa initialize et (şimdilik kapalı)
            if (_useTwilio)
            {
                // Twilio kodları burada olacak (şimdilik yorum satırı)
                // TwilioClient.Init(cfg["Twilio:AccountSid"], cfg["Twilio:AuthToken"]);
            }
        }

        [ExceptionHandlingAspect(customErrorMessage: "OTP gönderilemedi. Lütfen daha sonra tekrar deneyin.")]
        public async Task<IResult> SendAsync(string e164)
        {
            // Geçici olarak Twilio devre dışı - Default kod sistemi
            if (!_useTwilio)
            {
                // Default kod sistemi - her zaman başarılı döner
                // Gerçek SMS gönderilmez, sadece başarı mesajı döner
                await Task.CompletedTask; // Async uyumluluk için
                return new SuccessResult($"OTP gönderildi. (Development modu - Kod: {DEFAULT_OTP_CODE})");
            }

            // Twilio kodu burada olacak (şimdilik yorum satırı)
            // Twilio kodları buraya eklenecek
            return new ErrorResult("Twilio şu anda devre dışı. Default kod kullanın.");
        }

        [ExceptionHandlingAspect(customErrorMessage: "Doğrulama başarısız. Lütfen tekrar deneyin.")]
        public async Task<IResult> CheckAsync(string e164, string code)
        {
            // Geçici olarak Twilio devre dışı - Default kod kontrolü
            if (!_useTwilio)
            {
                await Task.CompletedTask; // Async uyumluluk için
                
                // Default kod kontrolü
                if (code == DEFAULT_OTP_CODE)
                {
                    return new SuccessResult("Doğrulandı.");
                }
                else
                {
                    return new ErrorResult($"Geçersiz kod. (Development modu - Doğru kod: {DEFAULT_OTP_CODE})");
                }
            }

            // Twilio kodu burada olacak (şimdilik yorum satırı)
            // Twilio kodları buraya eklenecek
            return new ErrorResult("Twilio şu anda devre dışı. Default kod kullanın.");
        }
    }
}
