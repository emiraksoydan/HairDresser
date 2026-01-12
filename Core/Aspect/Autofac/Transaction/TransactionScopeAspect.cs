using Autofac;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using Core.Utilities.IoC;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Core.Abstract;

namespace Core.Aspect.Autofac.Transaction
{

    public class TransactionScopeAspect : MethodInterception
    {
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
        public TransactionScopeOption ScopeOption { get; set; } = TransactionScopeOption.Required;
        public int TimeoutSeconds { get; set; } = 0;

        /// <summary>
        /// Transaction commit sonrası badge update'leri çalıştır
        /// </summary>
        private void ProcessBadgeUpdatesAfterCommit()
        {
            try
            {
                // ÖNEMLİ: Önce AspectInterceptorSelector.LifetimeScope'u dene (request scope)
                // Eğer null ise ServiceTool.ServiceProvider'ı kullan (root container)
                IBadgeUpdateService? badgeUpdateService = null;
                
                var lifetimeScope = AspectInterceptorSelector.LifetimeScope;
                if (lifetimeScope != null && lifetimeScope.TryResolve<IBadgeUpdateService>(out var resolvedFromScope))
                {
                    badgeUpdateService = resolvedFromScope;
                }
                else
                {
                    // Fallback: ServiceTool.ServiceProvider kullan (root container)
                    var serviceProvider = ServiceTool.ServiceProvider;
                    if (serviceProvider != null)
                    {
                        badgeUpdateService = serviceProvider.GetService<IBadgeUpdateService>();
                    }
                }
                
                if (badgeUpdateService != null)
                {
                    // Background task olarak çalıştır - transaction commit edildikten sonra
                    // ÖNEMLİ: Transaction gerçekten commit edilene kadar kısa bir bekleme ekle
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Transaction commit edilene kadar bekle (100ms - daha hızlı yanıt için optimize edildi)
                            await Task.Delay(100);
                            
                            // Retry mekanizması: İlk denemede başarısız olursa tekrar dene
                            // Daha agresif retry stratejisi - badge count'un anlık güncellenmesi için
                            const int maxRetries = 5;
                            const int initialRetryDelay = 50;
                            
                            for (int attempt = 0; attempt < maxRetries; attempt++)
                            {
                                try
                                {
                                    // BadgeUpdateService aynı request'in instance'ı kullanılır (InstancePerLifetimeScope)
                                    await badgeUpdateService.ProcessScheduledBadgeUpdatesAsync();
                                    break; // Başarılı olursa döngüden çık
                                }
                                catch
                                {
                                    // Son deneme değilse bekle ve tekrar dene (exponential backoff)
                                    if (attempt < maxRetries - 1)
                                    {
                                        var delay = initialRetryDelay * (int)Math.Pow(2, attempt);
                                        await Task.Delay(delay);
                                    }
                                    // Son denemede de başarısız olursa sessizce devam et (kritik değil)
                                }
                            }
                        }
                        catch
                        {
                            // Badge update başarısız olursa sessizce devam et (kritik değil)
                        }
                    });
                }
            }
            catch
            {
                // BadgeUpdateService resolve edilemezse sessizce devam et (kritik değil)
            }
        }
        public override void Intercept(IInvocation invocation)
        {
            var returnType = invocation.MethodInvocationTarget.ReturnType;
            if (typeof(Task).IsAssignableFrom(returnType))
            {
                if (returnType.IsGenericType) // Task<T>
                {
                    var tArg = returnType.GetGenericArguments()[0];
                    var method = typeof(TransactionScopeAspect)
                        .GetMethod(nameof(InterceptAsyncWithResult), BindingFlags.Instance | BindingFlags.NonPublic)!
                        .MakeGenericMethod(tArg);

                    invocation.ReturnValue = method.Invoke(this, new object[] { invocation });
                }
                else // Task
                {
                    invocation.ReturnValue = InterceptAsync(invocation);
                }
                return;
            }
            using (var scope = CreateScope())
            {
                invocation.Proceed();
                scope.Complete();
            }

            ProcessBadgeUpdatesAfterCommit();
        }
        private TransactionScope CreateScope()
        {
            var txOptions = new TransactionOptions
            {
                IsolationLevel = IsolationLevel,
                Timeout = TimeoutSeconds > 0
                ? TimeSpan.FromSeconds(TimeoutSeconds)
                : TransactionManager.DefaultTimeout
            };

            return new TransactionScope(
                ScopeOption,
                txOptions,
                TransactionScopeAsyncFlowOption.Enabled // kritik!
            );
        }

        private async Task InterceptAsync(IInvocation invocation)
        {
            using (var scope = CreateScope())
            {
                invocation.Proceed(); // hedef metodu çağır
                var task = (Task)invocation.ReturnValue;
                await task.ConfigureAwait(false);
                scope.Complete();
            }

            ProcessBadgeUpdatesAfterCommit();
        }

        private async Task<T> InterceptAsyncWithResult<T>(IInvocation invocation)
        {
            T result;
            using (var scope = CreateScope())
            {
                invocation.Proceed();
                var task = (Task<T>)invocation.ReturnValue;
                result = await task.ConfigureAwait(false);
                scope.Complete();
            }

            ProcessBadgeUpdatesAfterCommit();
            return result;
        }
    }
}
