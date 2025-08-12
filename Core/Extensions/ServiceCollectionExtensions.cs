using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Extensions
{
    public static class ServiceCollectionExtentions
    {
        public static IServiceCollection AddDependencyResolvers(this IServiceCollection servicecollection, ICoreModule[] modules)
        {
            foreach (var module in modules)
            {
                module.Load(servicecollection);
            }
            return ServiceTool.Create(servicecollection);
        }
    }
}
