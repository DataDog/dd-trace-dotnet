using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Datadog.Trace.AspNetCore
{
    /// <summary>
    /// This class contains extension method to enable Datadog Trace in an ASP.NET Core application
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Enable Datadog Trace
        /// </summary>
        /// <param name="services">The services.</param>
        /// <returns>The <see cref="IServiceCollection"/></returns>
        public static IServiceCollection AddDatadogTrace(this IServiceCollection services)
        {
            services.AddSingleton<IStartupFilter, DatadogTraceStartupFilter>();
            return services;
        }
    }
}
