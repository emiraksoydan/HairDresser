using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Extensions
{
    /// <summary>
    /// Extension methods for IServiceCollection
    /// </summary>
    public static class ServiceCollectionExtentions
    {
        /// <summary>
        /// Adds dependency resolvers from core modules
        /// </summary>
        /// <param name="servicecollection">The service collection</param>
        /// <param name="modules">Array of core modules to load</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddDependencyResolvers(this IServiceCollection servicecollection, ICoreModule[] modules)
        {
            foreach (var module in modules)
            {
                module.Load(servicecollection);
            }
            // ServiceTool.Create() removed - Service Locator anti-pattern eliminated
            // All dependencies should be injected via constructor
            return servicecollection;
        }
    }
}
