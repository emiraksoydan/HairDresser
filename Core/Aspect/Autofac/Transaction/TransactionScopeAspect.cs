using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Core.Aspect.Autofac.Transaction
{

    public class TransactionScopeAspect : MethodInterception
    {
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
        public TransactionScopeOption ScopeOption { get; set; } = TransactionScopeOption.Required;
        public int TimeoutSeconds { get; set; } = 0;
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
            using var scope = CreateScope();
            invocation.Proceed();
            scope.Complete();


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
            using var scope = CreateScope();
            invocation.Proceed(); // hedef metodu çağır
            var task = (Task)invocation.ReturnValue;
            await task.ConfigureAwait(false);
            scope.Complete();
        }

        private async Task<T> InterceptAsyncWithResult<T>(IInvocation invocation)
        {
            using var scope = CreateScope();
            invocation.Proceed();
            var task = (Task<T>)invocation.ReturnValue;
            var result = await task.ConfigureAwait(false);
            scope.Complete();
            return result;
        }
    }
}
