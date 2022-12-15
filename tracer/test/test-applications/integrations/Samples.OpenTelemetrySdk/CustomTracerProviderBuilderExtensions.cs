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
                opt.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        }

        return builder;
    }
}
