using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Core.Exceptions;

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
