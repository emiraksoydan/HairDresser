using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using Microsoft.EntityFrameworkCore;
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
            
            // Save all DbContext changes before completing transaction
            await SaveAllDbContextChangesAsync(invocation);
            
            scope.Complete();
        }

        private async Task<T> InterceptAsyncWithResult<T>(IInvocation invocation)
        {
            using var scope = CreateScope();
            invocation.Proceed();
            var task = (Task<T>)invocation.ReturnValue;
            var result = await task.ConfigureAwait(false);
            
            // Save all DbContext changes before completing transaction
            await SaveAllDbContextChangesAsync(invocation);
            
            scope.Complete();
            return result;
        }

        /// <summary>
        /// Finds all DbContext instances in the invocation target (including DAL instances) and saves changes
        /// </summary>
        private async Task SaveAllDbContextChangesAsync(IInvocation invocation)
        {
            var target = invocation.InvocationTarget;
            if (target == null) return;

            var dbContexts = new HashSet<DbContext>();

            // Find DbContext in target's fields and properties
            FindDbContextsInObject(target, dbContexts);

            // Find DbContext in DAL instances (DALs have Context property or _context field)
            var targetType = target.GetType();
            var allFields = targetType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var allProperties = targetType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var field in allFields)
            {
                var fieldValue = field.GetValue(target);
                if (fieldValue != null)
                {
                    // Check if it's a DAL (has Context property or _context field)
                    FindDbContextsInDAL(fieldValue, dbContexts);
                    // Also check if it's directly a DbContext
                    if (fieldValue is DbContext dbContext)
                    {
                        dbContexts.Add(dbContext);
                    }
                }
            }

            foreach (var prop in allProperties.Where(p => p.CanRead))
            {
                var propValue = prop.GetValue(target);
                if (propValue != null)
                {
                    // Check if it's a DAL
                    FindDbContextsInDAL(propValue, dbContexts);
                    // Also check if it's directly a DbContext
                    if (propValue is DbContext dbContext)
                    {
                        dbContexts.Add(dbContext);
                    }
                }
            }

            // Save changes for all unique DbContext instances
            foreach (var dbContext in dbContexts)
            {
                if (dbContext.ChangeTracker.HasChanges())
                {
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        /// <summary>
        /// Recursively finds DbContext instances in an object (including nested DALs)
        /// </summary>
        private void FindDbContextsInObject(object obj, HashSet<DbContext> dbContexts)
        {
            if (obj == null) return;

            var objType = obj.GetType();

            // Check if it's a DbContext
            if (obj is DbContext dbContext)
            {
                dbContexts.Add(dbContext);
                return;
            }

            // Check if it's a DAL (has Context property or _context field)
            FindDbContextsInDAL(obj, dbContexts);
        }

        /// <summary>
        /// Finds DbContext in DAL instances (checks Context property and _context field)
        /// </summary>
        private void FindDbContextsInDAL(object dal, HashSet<DbContext> dbContexts)
        {
            if (dal == null) return;

            var dalType = dal.GetType();

            // Check for Context property (protected property in EfEntityRepositoryBase)
            var contextProp = dalType.GetProperty("Context", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (contextProp != null && contextProp.CanRead)
            {
                var contextValue = contextProp.GetValue(dal);
                if (contextValue is DbContext dbContext)
                {
                    dbContexts.Add(dbContext);
                }
            }

            // Check for _context field (private field in some DAL implementations)
            var contextField = dalType.GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (contextField != null)
            {
                var contextValue = contextField.GetValue(dal);
                if (contextValue is DbContext dbContext)
                {
                    dbContexts.Add(dbContext);
                }
            }

            // Also check for context field (lowercase)
            var contextFieldLower = dalType.GetField("context", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (contextFieldLower != null)
            {
                var contextValue = contextFieldLower.GetValue(dal);
                if (contextValue is DbContext dbContext)
                {
                    dbContexts.Add(dbContext);
                }
            }
        }
    }
}
