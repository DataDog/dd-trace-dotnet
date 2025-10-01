using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using Grpc.Net.Client;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

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

public static class CustomMeterProviderBuilderExtensions
{
    public static MeterProviderBuilder AddOtlpExporterIfEnvironmentVariablePresent(this MeterProviderBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("OTEL_METRICS_EXPORTER_ENABLED") is string value
        && value == "true")
        {
            builder.AddMeter("OpenTelemetryMetricsMeter");

            // Check if using Unix Domain Sockets
            var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
            var useUds = endpoint != null && endpoint.StartsWith("unix://", StringComparison.OrdinalIgnoreCase);

            if (useUds && !OperatingSystem.IsWindows())
            {
                var udsPath = new Uri(endpoint).AbsolutePath;

                // Allow h2c (HTTP/2 without TLS) for gRPC over UDS
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                return builder.AddOtlpExporter(o =>
                {
                    o.Protocol = OtlpExportProtocol.Grpc;

                    // gRPC requires an http/https authority; actual connection goes via ConnectCallback
                    o.Endpoint = new Uri("http://localhost");

                    o.HttpClientFactory = () =>
                    {
                        var handler = new SocketsHttpHandler
                        {
                            ConnectCallback = async (ctx, ct) =>
                            {
                                var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                                await sock.ConnectAsync(new UnixDomainSocketEndPoint(udsPath), ct).ConfigureAwait(false);
                                return new NetworkStream(sock, ownsSocket: true);
                            }
                        };

                        return new HttpClient(handler, disposeHandler: true);
                    };
                });
            }
            else
            {
                // Normal env-var driven exporter (grpc/http) for TCP
                return builder.AddOtlpExporter();
            }
        }

        return builder;
    }
}
