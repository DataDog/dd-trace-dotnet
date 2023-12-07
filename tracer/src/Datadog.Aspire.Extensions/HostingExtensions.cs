// <copyright file="HostingExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Datadog.Aspire.Extensions;

/// <summary>
/// Provides extension methods for configuring Datadog in the Aspire host.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Configures OTLP exporters
    /// </summary>
    /// <param name="builder">The host's application builder.</param>
    /// <param name="protocol">The protocol to use for the OTLP receiver.</param>
    /// <param name="port">The specified port to use for OTLP receiver.</param>
    /// <returns>The <see cref="IHostApplicationBuilder"/>. </returns>
    public static IHostApplicationBuilder AddOpenTelemetryExportersToDatadog(this IHostApplicationBuilder builder, OtlpExportProtocol? protocol = null, string? port = null)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            var otlpEndpointURL = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (otlpEndpointURL is not null)
            {
                if (protocol is null)
                {
                    var envProtocol = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]?.ToLowerInvariant();
                    protocol = envProtocol switch
                    {
                        "grpc" => OtlpExportProtocol.Grpc,
                        "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
                        _ => OtlpExportProtocol.Grpc,
                    };
                }

                if (port is null)
                {
                    port = protocol switch
                    {
                        OtlpExportProtocol.Grpc => "4317",
                        OtlpExportProtocol.HttpProtobuf => "4318",
                        _ => "4317",
                    };
                }

                // Add another OTLP exporter to send to Datadog.
                // Use the host specified in the OTEL_EXPORTER_OTLP_ENDPOINT URL
                // because Aspire should have now resolved the host name,
                // for either container-based deployments or process-based deployments
                var portIndex = otlpEndpointURL.LastIndexOf(':');
                var datadogOtlpEndpoint = $"{otlpEndpointURL.Substring(startIndex: 0, length: portIndex)}:{port}";

                builder.Services.Configure<OpenTelemetryLoggerOptions>(logging =>
                {
                    logging.AddOtlpExporter(options =>
                    {
                        var path = protocol == OtlpExportProtocol.HttpProtobuf ? "/v1/logs" : string.Empty;

                        options.Endpoint = new Uri(datadogOtlpEndpoint + path);
                        options.Protocol = (OtlpExportProtocol)protocol;
                    });
                });

                builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        var path = protocol == OtlpExportProtocol.HttpProtobuf ? "/v1/traces" : string.Empty;

                        options.Endpoint = new Uri(datadogOtlpEndpoint + path);
                        options.Protocol = (OtlpExportProtocol)protocol;
                    });
                });
            }
        }

        return builder;
    }
}
