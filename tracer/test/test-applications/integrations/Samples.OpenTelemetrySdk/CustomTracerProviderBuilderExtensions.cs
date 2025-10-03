using System;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
#if OTEL_1_2
using OpenTelemetry.Metrics;
#endif

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
