using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Autofac;

namespace Core.Utilities.Interceptors
{
    public class AspectInterceptorSelector : IInterceptorSelector
    {
        // Static property to hold Autofac container for resolving interceptors with DI
        // This is set in Program.cs after container is built
        public static ILifetimeScope? LifetimeScope { get; set; }

        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
        {
            var classAttributes = type.GetCustomAttributes<MethodInterceptionBase>(true).ToList();
            var methodAttributes = type.GetMethod(method.Name)
         .GetCustomAttributes<MethodInterceptionBase>(true);
            classAttributes.AddRange(methodAttributes);

            // Try to resolve interceptors from Autofac container if available
            // This allows interceptors to use constructor injection (e.g., SecuredOperation)
            var resolvedInterceptors = new List<IInterceptor>();
            
            foreach (var attribute in classAttributes.OrderBy(x => x.Priority))
            {
                IInterceptor? interceptor = null;
                
                // Try to resolve from Autofac container first (for DI support)
                if (LifetimeScope != null)
                {
                    try
                    {
                        // Try to resolve the interceptor type from Autofac
                        // This allows constructor injection to work
                        var interceptorType = attribute.GetType();
                        if (LifetimeScope.TryResolve(interceptorType, out var resolved))
                        {
                            interceptor = resolved as IInterceptor;
                        }
                    }
                    catch
                    {
                        // If resolution fails, fall back to using the attribute directly
                    }
                }
                
                // Fallback: Use attribute directly if not resolved from container
                // This works for interceptors that don't need DI (e.g., LogAspect, ValidationAspect)
                if (interceptor == null)
                {
                    interceptor = attribute as IInterceptor;
                }
                
                if (interceptor != null)
                {
                    resolvedInterceptors.Add(interceptor);
                }
            }

            return resolvedInterceptors.ToArray();
        }
    }
}
