using Castle.DynamicProxy;
using Core.Exceptions;
using Core.Extensions;
using Core.Utilities.Interceptors;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace Business.BusinessAspect.Autofac
{
    public class SecuredOperation : MethodInterception
    {
        private readonly string[] _roles;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SecuredOperation(string roles, IHttpContextAccessor httpContextAccessor)
        {
            _roles = roles.Split(',');
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }
        protected override void OnBefore(IInvocation invocation)
        {
            var roleClaims = _httpContextAccessor.HttpContext.User.ClaimRoles();
            if (!_roles.Any(requiredRole => roleClaims.Contains(requiredRole)))
            {
                throw new UnauthorizedOperationException(Business.Resources.Messages.UnauthorizedOperation);
            }
        }
    }
}
