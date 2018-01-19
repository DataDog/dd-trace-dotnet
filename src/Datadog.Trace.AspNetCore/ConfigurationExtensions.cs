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
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="tracer">The tracer.</param>
        /// <param name="serviceName">The service name that will be set on the spans created by the instrumentation</param>
        /// <returns>The <see cref="IServiceCollection"/></returns>
        public static IServiceCollection AddDatadogTrace(this IServiceCollection services, Tracer tracer = null, string serviceName = null)
        {
            tracer = tracer ?? Tracer.Instance;
            services.AddSingleton(tracer);
            services.AddSingleton(new AspNetCoreListenerConfig { ServiceName = serviceName });
            services.AddSingleton<AspNetCoreListener, AspNetCoreListener>();
            services.AddSingleton<IStartupFilter, DatadogTraceStartupFilter>();
            return services;
        }
    }
}
