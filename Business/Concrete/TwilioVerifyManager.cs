using Business.Abstract;
using Core.Utilities.Results;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Verify.V2.Service;

namespace Business.Concrete
{
    public class TwilioVerifyManager : ITwilioVerifyService
    {
        private readonly string _serviceSid;
        public TwilioVerifyManager(IConfiguration cfg)
        {
            TwilioClient.Init(cfg["Twilio:AccountSid"], cfg["Twilio:AuthToken"]);
            _serviceSid = cfg["Twilio:VerifyServiceSid"]!;
        }

        public async Task<IResult> SendAsync(string e164)
        {
            try
            {
                var v = await VerificationResource.CreateAsync(
                    to: e164,
                    channel: "sms",
                    pathServiceSid: _serviceSid      
                );
                return v.Status is "pending" or "approved"
                    ? new SuccessResult("OTP gönderildi.")
                    : v.Status is "failed" ? new ErrorResult("SMS kullanıcıya ulaştırılamadı") :  new ErrorResult("OTP gönderilemedi.");
            }
            catch (Exception ex) {
                string userFriendly;
                if (ex.Message.Contains("unverified", StringComparison.OrdinalIgnoreCase))
                    userFriendly = "Telefon numarası doğrulanmamış. Twilio deneme hesapları yalnızca doğrulanmış numaralara SMS gönderebilir.";
                else if (ex.Message.Contains("Permission to send an SMS has not been enabled"))
                    userFriendly = "SMS gönderim izni etkinleştirilmemiş. Twilio kontrol panelinden SMS iznini açın.";
                else
                    userFriendly = "OTP gönderilemedi. Lütfen daha sonra tekrar deneyin.";
                return new ErrorResult(userFriendly);
            }
        }

        public async Task<IResult> CheckAsync(string e164, string code)
        {
            try
            {
                var c = await VerificationCheckResource.CreateAsync(
                    to: e164, code: code, pathServiceSid: _serviceSid
                );
                return c.Status is "approved"
                    ? new SuccessResult("Doğrulandı.") : 
                    c.Status is "max_attempts_reached" ? new ErrorResult("Kullanıcı çok fazla yanlış kod girdi") 
                    :c.Status is "expired" ? new ErrorResult("OTP’nin geçerlilik süresi bitti.") : new ErrorResult("Doğrulanamadı");
            }
            catch (Exception ex) { return new ErrorResult($"Doğrulama başarısız: {ex.Message}"); }
        }
    }
}
