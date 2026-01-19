using Business.Resources;
using Castle.DynamicProxy;
using Core.Exceptions;
using Core.Extensions;
using Core.Utilities.Interceptors;
using Core.Utilities.IoC;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Business.BusinessAspect.Autofac
{
    public class SecuredOperation : MethodInterception
    {
        private readonly string[] _roles;
        private IHttpContextAccessor _httpContextAccessor;

        public SecuredOperation(string roles)
        {
            _roles = roles.Split(',').Select(r => r.Trim()).ToArray();
            _httpContextAccessor =  ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
        }

      protected override void OnBefore(IInvocation invocation)
        {
            // KRITĐK: HttpContext null kontrolü (BackgroundService veya non-HTTP context'te çalışabilir)
            if (_httpContextAccessor?.HttpContext == null)
                throw new UnauthorizedOperationException("HTTP context bulunamadı. Bu işlem sadece HTTP request içinde çalıştırılabilir.");

            // User null kontrolü
            if (_httpContextAccessor.HttpContext.User == null)
                throw new UnauthorizedOperationException("Kullanıcı bilgisi bulunamadı. Lütfen giriş yapın.");

            var roleClaims = _httpContextAccessor.HttpContext.User.ClaimRoles();
            
            // Role claims null veya boş kontrolü
            if (roleClaims == null || !roleClaims.Any())
                throw new UnauthorizedOperationException("Kullanıcı rolü bulunamadı. Lütfen yetkilendirme bilgilerinizi kontrol edin.");

            foreach (var role in _roles)
            {
                if (roleClaims.Contains(role))
                {
                    return;
                }
            }
            throw new UnauthorizedOperationException($"Bu işlem için gerekli yetkiye sahip değilsiniz. Gerekli roller: {string.Join(", ", _roles)}");
        }
    }
}
