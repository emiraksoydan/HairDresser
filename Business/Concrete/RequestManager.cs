using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    public class RequestManager : IRequestService
    {
        private readonly IRequestDal _requestDal;
        private readonly IUserDal _userDal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RequestManager> _logger;
        private readonly IContentModerationService _contentModeration;

        // Target email for requests
        private const string TARGET_EMAIL = "gumusmakastr@gmail.com";

        public RequestManager(
            IRequestDal requestDal,
            IUserDal userDal,
            IConfiguration configuration,
            ILogger<RequestManager> logger,
            IContentModerationService contentModeration)
        {
            _requestDal = requestDal;
            _userDal = userDal;
            _configuration = configuration;
            _logger = logger;
            _contentModeration = contentModeration;
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(CreateRequestDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<RequestGetDto>> CreateRequestAsync(Guid userId, CreateRequestDto dto)
        {
            // İçerik moderasyonu kontrolü
            var moderationResult = await _contentModeration.CheckContentsAsync(dto.RequestTitle, dto.RequestMessage);
            if (!moderationResult.Success)
                return new ErrorDataResult<RequestGetDto>(moderationResult.Message);

            var user = await _userDal.Get(x => x.Id == userId);
            if (user == null)
                return new ErrorDataResult<RequestGetDto>("Kullanıcı bulunamadı.");

            // Request oluştur
            var request = new Request
            {
                Id = Guid.NewGuid(),
                RequestFromUserId = userId,
                RequestTitle = dto.RequestTitle.Trim(),
                RequestMessage = dto.RequestMessage.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false
            };

            await _requestDal.Add(request);

            // Email gönder (async, hata durumunda işlemi etkilemesin)
            _ = SendEmailAsync(user, request);

            var result = new RequestGetDto
            {
                Id = request.Id,
                RequestFromUserId = request.RequestFromUserId,
                RequestTitle = request.RequestTitle,
                RequestMessage = request.RequestMessage,
                CreatedAt = request.CreatedAt,
                IsProcessed = request.IsProcessed
            };

            return new SuccessDataResult<RequestGetDto>(result, "İsteğiniz başarıyla gönderildi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<RequestGetDto>>> GetMyRequestsAsync(Guid userId)
        {
            var requests = await _requestDal.GetByUserAsync(userId);
            var result = new List<RequestGetDto>();

            foreach (var request in requests)
            {
                result.Add(new RequestGetDto
                {
                    Id = request.Id,
                    RequestFromUserId = request.RequestFromUserId,
                    RequestTitle = request.RequestTitle,
                    RequestMessage = request.RequestMessage,
                    CreatedAt = request.CreatedAt,
                    IsProcessed = request.IsProcessed
                });
            }

            return new SuccessDataResult<List<RequestGetDto>>(result);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> DeleteRequestAsync(Guid userId, Guid requestId)
        {
            var request = await _requestDal.Get(x => x.Id == requestId && !x.IsDeleted);
            if (request == null)
                return new ErrorDataResult<bool>(false, "İstek bulunamadı.");

            if (request.RequestFromUserId != userId)
                return new ErrorDataResult<bool>(false, "Bu isteği silme yetkiniz yok.");

            // Soft delete
            request.IsDeleted = true;
            request.DeletedAt = DateTime.UtcNow;
            await _requestDal.Update(request);
            return new SuccessDataResult<bool>(true, "İstek başarıyla silindi.");
        }

        private async Task SendEmailAsync(User user, Request request)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUser = _configuration["Email:SmtpUser"];
                var smtpPass = _configuration["Email:SmtpPass"];
                var fromEmail = _configuration["Email:FromEmail"];

                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser))
                {
                    _logger.LogWarning("Email configuration is missing. Skipping email send for request {RequestId}", request.Id);
                    return;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var userTypeStr = user.UserType switch
                {
                    Entities.Concrete.Enums.UserType.Customer => "Müşteri",
                    Entities.Concrete.Enums.UserType.FreeBarber => "Serbest Berber",
                    Entities.Concrete.Enums.UserType.BarberStore => "Dükkan Sahibi",
                    _ => "Bilinmeyen"
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail ?? smtpUser, "Berber Uygulaması"),
                    Subject = $"[İstek] {request.RequestTitle}",
                    Body = $@"
Yeni bir kullanıcı isteği alındı:

Gönderen: {user.FirstName} {user.LastName}
Telefon: {user.PhoneNumber}
Kullanıcı Tipi: {userTypeStr}
Kullanıcı ID: {user.Id}

Başlık: {request.RequestTitle}

Mesaj:
{request.RequestMessage}

---
Gönderim Tarihi: {request.CreatedAt:dd.MM.yyyy HH:mm}
İstek ID: {request.Id}
",
                    IsBodyHtml = false
                };
                mailMessage.To.Add(TARGET_EMAIL);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully for request {RequestId}", request.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email for request {RequestId}", request.Id);
                // Email gönderimi başarısız olsa da işlem devam etsin
            }
        }
    }
}
