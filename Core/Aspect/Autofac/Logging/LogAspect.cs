using Castle.DynamicProxy;
using Core.Extensions;
using Core.Utilities.Interceptors;
using Core.Utilities.IoC;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Core.Aspect.Autofac.Logging
{
    public class LogAspect : MethodInterception
    {
        private readonly bool _logParameters;
        private readonly bool _logReturnValue;
        private readonly string _logDirectory;


        public LogAspect(bool logParameters = true, bool logReturnValue = false, string logDirectory = "Logs")
        {
            _logParameters = logParameters;
            _logReturnValue = logReturnValue;
            
            // AppContext.BaseDirectory kullanarak uygulamanın root dizinine göre log klasörü oluştur
            // Bu şekilde development ve production'da tutarlı olur
            var baseDirectory = AppContext.BaseDirectory;
            _logDirectory = Path.IsPathRooted(logDirectory) 
                ? logDirectory 
                : Path.Combine(baseDirectory, logDirectory);

            // Log klasörünü oluştur
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        protected override void OnBefore(IInvocation invocation)
        {
            var methodName = GetMethodName(invocation);
            var className = invocation.TargetType.Name;
            var parameters = _logParameters ? GetParameters(invocation) : null;
            var userInfo = GetUserInfo();

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] Method started: {className}.{methodName}";
            if (!string.IsNullOrEmpty(userInfo))
            {
                logMessage += $" | User: {userInfo}";
            }
            if (_logParameters && parameters != null)
            {
                logMessage += $" | Parameters: {parameters}";
            }

            WriteLog(logMessage);
        }

        protected override void OnAfter(IInvocation invocation)
        {
            var methodName = GetMethodName(invocation);
            var className = invocation.TargetType.Name;
            var userInfo = GetUserInfo();

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] Method completed: {className}.{methodName}";
            if (!string.IsNullOrEmpty(userInfo))
            {
                logMessage += $" | User: {userInfo}";
            }

            WriteLog(logMessage);
        }

        protected override void OnException(IInvocation invocation, Exception exception)
        {
            var methodName = GetMethodName(invocation);
            var className = invocation.TargetType.Name;
            var parameters = _logParameters ? GetParameters(invocation) : null;
            var userInfo = GetUserInfo();

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Method failed: {className}.{methodName}";
            if (!string.IsNullOrEmpty(userInfo))
            {
                logMessage += $" | User: {userInfo}";
            }
            if (_logParameters && parameters != null)
            {
                logMessage += $" | Parameters: {parameters}";
            }
            logMessage += $" | Error: {exception.Message} | StackTrace: {exception.StackTrace}";

            WriteLog(logMessage);
        }

        protected override void OnSuccess(IInvocation invocation)
        {
            var methodName = GetMethodName(invocation);
            var className = invocation.TargetType.Name;
            var returnValue = _logReturnValue ? GetReturnValue(invocation) : null;
            var userInfo = GetUserInfo();

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] Method succeeded: {className}.{methodName}";
            if (!string.IsNullOrEmpty(userInfo))
            {
                logMessage += $" | User: {userInfo}";
            }
            if (_logReturnValue && returnValue != null)
            {
                logMessage += $" | ReturnValue: {returnValue}";
            }

            WriteLog(logMessage);
        }

        private void WriteLog(string message)
        {
            try
            {
                var logFileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
                var logFilePath = Path.Combine(_logDirectory, logFileName);

                // Thread-safe logging
                lock (this)
                {
                    File.AppendAllText(logFilePath, message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Log yazma hatası - sessizce devam et (loglamaya log yazamayız)
            }
        }

        private string GetMethodName(IInvocation invocation)
        {
            return $"{invocation.Method.Name}";
        }

        private string GetParameters(IInvocation invocation)
        {
            try
            {
                var parameters = new Dictionary<string, object?>();

                var methodParameters = invocation.Method.GetParameters();
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    var paramName = methodParameters[i].Name ?? $"param{i}";
                    var paramValue = invocation.Arguments[i];

                    // Sensitive data kontrolü - şifre, token vb. loglamayalım
                    if (IsSensitiveParameter(paramName))
                    {
                        parameters[paramName] = "***REDACTED***";
                    }
                    else
                    {
                        parameters[paramName] = paramValue;
                    }
                }

                return JsonSerializer.Serialize(parameters, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    MaxDepth = 3 // Çok derin nesneleri loglamayı sınırla
                });
            }
            catch (Exception ex)
            {
                return $"Error serializing parameters: {ex.Message}";
            }
        }

        private string GetReturnValue(IInvocation invocation)
        {
            try
            {
                var returnValue = invocation.ReturnValue;
                if (returnValue == null)
                {
                    return "null";
                }

                // Task kontrolü - Task'ları loglamayalım
                if (returnValue.GetType().IsAssignableFrom(typeof(System.Threading.Tasks.Task)))
                {
                    return "Task (async method)";
                }

                return JsonSerializer.Serialize(returnValue, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    MaxDepth = 2
                });
            }
            catch (Exception ex)
            {
                return $"Error serializing return value: {ex.Message}";
            }
        }

        private bool IsSensitiveParameter(string parameterName)
        {
            var sensitiveKeywords = new[] { "password", "pwd", "token", "secret", "key", "credential", "auth" };
            var lowerParamName = parameterName.ToLowerInvariant();
            return sensitiveKeywords.Any(keyword => lowerParamName.Contains(keyword));
        }

        private string GetUserInfo()
        {
            try
            {
                var httpContextAccessor = ServiceTool.ServiceProvider?.GetService<IHttpContextAccessor>();
                if (httpContextAccessor?.HttpContext?.User == null)
                {
                    return string.Empty;
                }

                var user = httpContextAccessor.HttpContext.User;
                var userInfoParts = new List<string>();

                // User ID
                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    userInfoParts.Add($"Id: {userId}");
                }

                // Email
                var email = user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    userInfoParts.Add($"Email: {email}");
                }

                // Name
                var name = user.FindFirst("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    userInfoParts.Add($"Name: {name}");
                }

                // Roles
                var roles = user.ClaimRoles();
                if (roles != null && roles.Any())
                {
                    userInfoParts.Add($"Roles: {string.Join(",", roles)}");
                }

                // UserType
                var userType = user.FindFirst("userType")?.Value;
                if (!string.IsNullOrEmpty(userType))
                {
                    userInfoParts.Add($"Type: {userType}");
                }

                return userInfoParts.Any() ? string.Join(", ", userInfoParts) : string.Empty;
            }
            catch
            {
                // Kullanıcı bilgisi alınamazsa sessizce devam et
                return string.Empty;
            }
        }
    }
}
