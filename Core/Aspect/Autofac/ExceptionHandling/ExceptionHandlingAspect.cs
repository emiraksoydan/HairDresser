using Castle.DynamicProxy;
using Core.Exceptions;
using Core.Utilities.Interceptors;
using Core.Utilities.Results;
using System;
using System.Threading.Tasks;

namespace Core.Aspect.Autofac.ExceptionHandling
{
    /// <summary>
    /// Exception handling aspect - Metodlarda oluşan hataları yakalar ve IResult/IDataResult döner
    /// Kullanım: [ExceptionHandlingAspect] veya [ExceptionHandlingAspect(customErrorMessage: "Özel mesaj")]
    /// </summary>
    public class ExceptionHandlingAspect : MethodInterception
    {
        public string? CustomErrorMessage { get; set; }

        public ExceptionHandlingAspect(string? customErrorMessage = null)
        {
            CustomErrorMessage = customErrorMessage;
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
                
                var handledResult = HandleException(e, invocation);
                if (handledResult != null)
                {
                    invocation.ReturnValue = handledResult;
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (isSuccess) OnSuccess(invocation);
            }
            
            OnAfter(invocation);
        }

        private object? HandleException(Exception exception, IInvocation invocation)
        {
            var returnType = invocation.Method.ReturnType;
            
            // Async metodlar için Task<T>
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var taskResultType = returnType.GetGenericArguments()[0];
                var result = CreateResultForType(exception, taskResultType);
                
                if (result != null)
                {
                    var fromResultMethod = typeof(Task).GetMethod("FromResult")?.MakeGenericMethod(taskResultType);
                    return fromResultMethod?.Invoke(null, new[] { result });
                }
            }
            
            // Task (void) için
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }
            
            // Senkron metodlar için
            return CreateResultForType(exception, returnType);
        }

        private object? CreateResultForType(Exception exception, Type returnType)
        {
            // IDataResult<T> kontrolü
            if (returnType.IsGenericType)
            {
                var genericTypeDefinition = returnType.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(IDataResult<>) || 
                    genericTypeDefinition.Name.StartsWith("IDataResult"))
                {
                    var dataType = returnType.GetGenericArguments()[0];
                    return CreateErrorDataResult(exception, dataType);
                }
            }
            
            // IResult kontrolü
            if (typeof(IResult).IsAssignableFrom(returnType) && returnType != typeof(IResult))
            {
                return CreateErrorResult(exception);
            }
            
            return null;
        }

        private object CreateErrorDataResult(Exception exception, Type dataType)
        {
            var errorMessage = GetErrorMessage(exception);
            var errorDataResultType = typeof(ErrorDataResult<>).MakeGenericType(dataType);
            var defaultData = dataType.IsValueType ? Activator.CreateInstance(dataType) : null;
            
            return Activator.CreateInstance(errorDataResultType, defaultData, errorMessage) 
                ?? throw new InvalidOperationException($"ErrorDataResult<{dataType.Name}> oluşturulamadı");
        }

        private object CreateErrorResult(Exception exception)
        {
            return new ErrorResult(GetErrorMessage(exception));
        }

        private string GetErrorMessage(Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(CustomErrorMessage))
            {
                return CustomErrorMessage;
            }
            
            return exception switch
            {
                UnauthorizedOperationException => exception.Message,
                ArgumentNullException => exception.Message ?? "Geçersiz parametre",
                ArgumentException => exception.Message,
                InvalidOperationException => exception.Message,
                _ => exception.Message ?? "Bir hata oluştu. Lütfen tekrar deneyin."
            };
        }
    }
}
