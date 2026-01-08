using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Core.Extensions
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                // FluentValidation.ValidationException

                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";

                var errors = ex.Errors.Select(e => new
                {
                    field = e.PropertyName,
                    message = e.ErrorMessage
                });

                var payload = new
                {
                    success = false,
                    message = "Doğrulama hatası",
                    errors = errors
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
            catch (BusinessRuleException ex)
            {
                // Business rule violation exception

                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    success = false,
                    message = ex.Message
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
            catch (EntityNotFoundException ex)
            {
                // Entity not found exception

                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    success = false,
                    message = ex.Message
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
            catch (UnauthorizedOperationException ex)
            {
                // Unauthorized operation exception

                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    success = false,
                    message = ex.Message
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                // PostgreSQL database exception handling
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";

                string userMessage;
                switch (pgEx.SqlState)
                {
                    case "23505": // unique_violation
                        userMessage = "Bu işlem için zaten bir kayıt mevcut. Lütfen farklı bir değer deneyin.";
                        break;
                    case "23503": // foreign_key_violation
                        userMessage = "İlgili kayıt bulunamadı. Lütfen geçerli bir değer seçin.";
                        break;
                    case "23502": // not_null_violation
                        userMessage = "Zorunlu alanlar eksik. Lütfen tüm gerekli bilgileri doldurun.";
                        break;
                    case "23514": // check_violation
                        userMessage = "Girilen değer geçersiz. Lütfen kontrol edin.";
                        break;
                    case "23506": // exclusion_violation
                        userMessage = "Bu işlem için çakışan bir kayıt mevcut.";
                        break;
                    default:
                        userMessage = "Veritabanı hatası oluştu. Lütfen tekrar deneyin.";
                        break;
                }

                var payload = new
                {
                    success = false,
                    message = userMessage
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
            catch (Exception ex)
            {
                // Diğer tüm beklenmeyen hatalar
     
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    success = false,
                    message = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyiniz."
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
