using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Core.Extensions
{
    /// <summary>
    /// Global exception middleware - Tüm hataları yakalar ve tutarlı JSON response döner
    /// Response format: { success: false, message: "...", errors?: [...] }
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public GlobalExceptionMiddleware(RequestDelegate next)
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
                // FluentValidation hataları - 400 Bad Request
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, "Doğrulama hatası", 
                    ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }));
            }
            catch (UnauthorizedOperationException ex)
            {
                // Yetki hataları - 403 Forbidden
                await WriteErrorResponse(context, HttpStatusCode.Forbidden, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Genel yetki hataları - 403 Forbidden
                await WriteErrorResponse(context, HttpStatusCode.Forbidden, ex.Message);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                // PostgreSQL veritabanı hataları - 400 Bad Request
                var userMessage = pgEx.SqlState switch
                {
                    "23505" => "Bu işlem için zaten bir kayıt mevcut. Lütfen farklı bir değer deneyin.",
                    "23503" => "İlgili kayıt bulunamadı. Lütfen geçerli bir değer seçin.",
                    "23502" => "Zorunlu alanlar eksik. Lütfen tüm gerekli bilgileri doldurun.",
                    "23514" => "Girilen değer geçersiz. Lütfen kontrol edin.",
                    "23506" => "Bu işlem için çakışan bir kayıt mevcut.",
                    _ => "Veritabanı hatası oluştu. Lütfen tekrar deneyin."
                };
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, userMessage);
            }
            catch (ArgumentException ex)
            {
                // Argüman hataları - 400 Bad Request
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Geçersiz işlem hataları - 400 Bad Request
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                await WriteErrorResponse(context, HttpStatusCode.InternalServerError,ex.Message);

                // Diğer tüm beklenmeyen hatalar - 500 Internal Server Error
                await WriteErrorResponse(context, HttpStatusCode.InternalServerError, 
                    "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyiniz.");
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message, object? errors = null)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var payload = errors != null
                ? new { success = false, message, errors }
                : new { success = false, message, errors = (object?)null };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, options));
        }
    }
}
