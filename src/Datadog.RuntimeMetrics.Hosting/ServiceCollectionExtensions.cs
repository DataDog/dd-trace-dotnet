using System;
using Datadog.Trace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StatsdClient;

namespace Datadog.RuntimeMetrics.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatadogTracing(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddDatadogTracing(Tracer.Instance);
            return services;
        }

        public static IServiceCollection AddDatadogTracing(this IServiceCollection services, Tracer tracer)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (tracer == null)
            {
                throw new ArgumentNullException(nameof(tracer));
            }

            if (!ReferenceEquals(Tracer.Instance, tracer))
            {
                Tracer.Instance = tracer;
            }

            services.AddOptions();
            services.TryAddSingleton(tracer);
            return services;
        }

        public static IServiceCollection AddDogStatsd(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions();
            services.TryAddTransient<IStatsdUDP, StatsdUdpWrapper>();
            return services;
        }

        public static IServiceCollection AddDatadogRuntimeMetrics(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions();
            services.AddDogStatsd();

            services.TryAddTransient<GcMetricsBackgroundService>();
            services.TryAddTransient<StatsdMetricsSubscriberWrapper>();
            services.AddHostedService<GcMetricsHostedService>();
            return services;
        }

        public static IServiceCollection AddDatadogRuntimeMetrics(this IServiceCollection services, Action<StatsdMetricsOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddDatadogRuntimeMetrics();
            services.Configure(setupAction);
            return services;
        }
    }
}
