using System;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
#if OTEL_1_2
using OpenTelemetry.Metrics;
#endif
#if OTEL_1_9
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
#endif
using Microsoft.Extensions.Logging;

namespace Samples.OpenTelemetrySdk;
public static class CustomTracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddOtlpExporterIfEnvironmentVariablePresent(this TracerProviderBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENABLED") is string value
        && value == "true")
        {
            return builder.AddOtlpExporter(opt =>
            {
#if OTEL_1_2
                opt.Protocol = OtlpExportProtocol.HttpProtobuf;
#endif
            });
        }

        return builder;
    }

    public static TracerProviderBuilder AddActivitySourceIfEnvironmentVariablePresent(this TracerProviderBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("ADD_ADDITIONAL_ACTIVITY_SOURCE") is string value
&& value == "true")
        {
            return builder.AddSource(Program._additionalActivitySourceName);
        }
        
        return builder;
    }
}

#if OTEL_1_2
public static class CustomMeterProviderBuilderExtensions
{
    public static MeterProviderBuilder AddOtlpExporterIfEnvironmentVariablePresent(this MeterProviderBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("OTEL_METRICS_EXPORTER_ENABLED") is string value
        && value == "true")
        {
            return builder
                  .AddMeter("OpenTelemetryMetricsMeter")
                  .AddOtlpExporter();
        }

        return builder;
    }
}
#endif

#if OTEL_1_9
public static class CustomLoggerFactoryBuilderExtensions
{
    // Returns an IServiceProvider rather than an ILoggerFactory so callers can resolve
    // the underlying OpenTelemetry.Logs.LoggerProvider and call Shutdown before process exit.
    // LoggerProviderSdk.Dispose() caps its shutdown flush at 5s; with gRPC, the first export
    // can exceed that due to TCP/HTTP/2/TLS handshake, causing batched logs to be dropped.
    public static ServiceProvider CreateLoggerServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);

            if (Environment.GetEnvironmentVariable("OTEL_LOGS_EXPORTER_ENABLED") is string value
                && value == "true")
            {
#if NET6_0_OR_GREATER
                builder.AddOpenTelemetry(options =>
                {
                    options.AddOtlpExporter();
                });
#endif
            }
        });

        return services.BuildServiceProvider();
    }
}
#endif
