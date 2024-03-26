using System;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Samples.AzureServiceBus;

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

    public static TracerProviderBuilder AddAzureServiceBusIfEnvironmentVariablePresent(this TracerProviderBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("OTEL_ASB_ENABLED") is string value
        && value == "true")
        {
            return builder.AddSource("Azure.*");
        }

        return builder;
    }
}
