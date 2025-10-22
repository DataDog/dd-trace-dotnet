using System;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
#if OTEL_1_2
using OpenTelemetry.Metrics;
#endif
#if OTEL_1_9
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
    public static ILoggerFactory AddOtlpExporterIfEnvironmentVariablePresent()
    {
        // Check if OpenTelemetry Logs Exporter is enabled (similar to metrics)
        if (Environment.GetEnvironmentVariable("OTEL_LOGS_EXPORTER_ENABLED") is string value
        && value == "true")
        {
            return LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
#if NET6_0_OR_GREATER
                builder.AddOpenTelemetry(
                    options =>
                    {
                        options.AddOtlpExporter();
                    }
                );
#endif
            });
        }
        else
        {
            // Create logger factory without OTel - Datadog instrumentation will hook this
            return LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
            });
        }
    }
}
#endif
