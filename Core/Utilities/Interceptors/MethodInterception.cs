using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using Autofac;

namespace Core.Utilities.Interceptors
{
    public class MethodInterception : MethodInterceptionBase
    {
        protected virtual void OnBefore(IInvocation invocation) { }
        protected virtual void OnAfter(IInvocation invocation) { }
        protected virtual void OnException(IInvocation invocation, System.Exception e) { }
        protected virtual void OnSuccess(IInvocation invocation) { }
        
        /// <summary>
        /// Resolves IHttpContextAccessor from Autofac container via AspectInterceptorSelector
        /// This avoids static properties by using the container directly
        /// </summary>
        protected IHttpContextAccessor? GetHttpContextAccessor()
        {
            var lifetimeScope = AspectInterceptorSelector.LifetimeScope;
            if (lifetimeScope != null && lifetimeScope.TryResolve<IHttpContextAccessor>(out var httpContextAccessor))
            {
                return httpContextAccessor;
            }
            return null;
        }
        
        public override void Intercept(IInvocation invocation)
        {
            var isSuccess = true;
            OnBefore(invocation);
            try
            {
                invocation.Proceed();
            }
            catch (Exception e)
            {
                isSuccess = false;
                OnException(invocation, e);
                throw;
            }
            finally
            {
                if (isSuccess)
                {
                    OnSuccess(invocation);
                }
            }
            OnAfter(invocation);
        }
    }
}
