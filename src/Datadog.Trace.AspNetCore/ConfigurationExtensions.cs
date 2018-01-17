using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Datadog.Trace.AspNetCore
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection AddDatadogTrace(this IServiceCollection services)
        {
            services.AddSingleton<IStartupFilter, DatadogTraceStartupFilter>();
            return services;
        }
    }
}
