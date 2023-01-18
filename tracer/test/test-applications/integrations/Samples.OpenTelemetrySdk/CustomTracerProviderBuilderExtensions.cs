using System;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

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
