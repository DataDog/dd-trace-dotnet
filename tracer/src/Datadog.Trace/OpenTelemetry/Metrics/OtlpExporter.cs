// <copyright file="OtlpExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
#nullable enable

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// OTLP Exporter implementation that exports metrics using the OpenTelemetry Protocol.
    /// This is the concrete implementation of the MetricExporter base class that handles OTLP protocol specifics.
    /// This is a Push Metric Exporter that sends metrics via the OpenTelemetry Protocol.
    /// </summary>
    internal sealed class OtlpExporter : MetricExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpExporter));
        private readonly HttpClient _httpClient;
        private readonly OtlpGrpcExportClient? _grpcClient;
        private readonly Telemetry.Metrics.MetricTags.Protocol _protocolTag;
        private readonly Telemetry.Metrics.MetricTags.MetricEncoding _encodingTag;
        private readonly OtlpMetricsSerializer _serializer;
        private readonly Uri _endpoint;
        private readonly IReadOnlyDictionary<string, string> _headers;
        private readonly int _timeoutMs;
        private readonly Configuration.OtlpProtocol _protocol;

        public OtlpExporter(Configuration.TracerSettings settings)
        {
            _endpoint = settings.OtlpMetricsEndpoint;
            _headers = settings.OtlpMetricsHeaders;
            _timeoutMs = settings.OtlpMetricsTimeoutMs;
            _protocol = settings.OtlpMetricsProtocol;

            _protocolTag = _protocol switch
            {
                Configuration.OtlpProtocol.Grpc => Telemetry.Metrics.MetricTags.Protocol.Grpc,
                _ => Telemetry.Metrics.MetricTags.Protocol.Http
            };

            _encodingTag = _protocol switch
            {
                Configuration.OtlpProtocol.Grpc => Telemetry.Metrics.MetricTags.MetricEncoding.Protobuf,
                Configuration.OtlpProtocol.HttpProtobuf => Telemetry.Metrics.MetricTags.MetricEncoding.Protobuf,
                Configuration.OtlpProtocol.HttpJson => Telemetry.Metrics.MetricTags.MetricEncoding.Json,
                _ => Telemetry.Metrics.MetricTags.MetricEncoding.Protobuf
            };

            _serializer = new OtlpMetricsSerializer(settings);
            _httpClient = CreateHttpClient(_endpoint);

            if (_protocol == Configuration.OtlpProtocol.Grpc)
            {
                var opt = new OtlpExporterOptions
                {
                    Endpoint = _endpoint,
                    TimeoutMilliseconds = _timeoutMs,
                    Protocol = (OtlpExportProtocol)_protocol,
                    AppendSignalPathToEndpoint = true
                };

                foreach (var kvp in _headers)
                {
                    opt.Headers[kvp.Key] = kvp.Value;
                }

                const string metricsGrpcPath = "opentelemetry.proto.collector.metrics.v1.MetricsService/Export";
                _grpcClient = new OtlpGrpcExportClient(opt, _httpClient, metricsGrpcPath);
            }
        }

        private delegate HttpRequestMessage HttpRequestFactory(byte[] payload, Uri endpoint, IReadOnlyDictionary<string, string> headers);

        /// <summary>
        /// Exports a batch of metrics using OTLP protocol asynchronously.
        /// This is the preferred method for better performance.
        /// </summary>
        /// <param name="metrics">Batch of metrics to export</param>
        /// <returns>ExportResult indicating success or failure</returns>
        public override async Task<ExportResult> ExportAsync(IReadOnlyList<MetricPoint> metrics)
        {
            if (metrics.Count == 0)
            {
                return ExportResult.Success;
            }

            TelemetryFactory.Metrics.RecordCountMetricsExportAttempts(_protocolTag, _encodingTag);

            try
            {
                var success = await SendOtlpRequest(metrics).ConfigureAwait(false);

                if (success)
                {
                    TelemetryFactory.Metrics.RecordCountMetricsExportSuccesses(_protocolTag, _encodingTag);
                    return ExportResult.Success;
                }
                else
                {
                    TelemetryFactory.Metrics.RecordCountMetricsExportFailures(_protocolTag, _encodingTag);
                    return ExportResult.Failure;
                }
            }
            catch (Exception ex)
            {
                // Seeing network connectivity errors so skipping telemetry
                Log.ErrorSkipTelemetry(ex, "Error exporting OTLP metrics.");
                TelemetryFactory.Metrics.RecordCountMetricsExportFailures(_protocolTag, _encodingTag);
                return ExportResult.Failure;
            }
        }

        /// <summary>
        /// Shuts down the exporter and ensures all pending exports complete.
        /// </summary>
        /// <param name="timeoutMilliseconds">Maximum time to wait for shutdown</param>
        /// <returns>True if shutdown completed successfully, false otherwise</returns>
        public override bool Shutdown(int timeoutMilliseconds)
        {
            try
            {
                _httpClient.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during OTLP exporter shutdown");
                return false;
            }
        }

        /// <summary>
        /// Creates an HttpClient with Unix Domain Socket support if the endpoint uses unix:// scheme.
        /// For TCP/IP endpoints (http:// or https://), creates a standard HttpClient with HTTP/2.
        /// </summary>
        private static HttpClient CreateHttpClient(Uri endpoint)
        {
            if (endpoint.Scheme == "unix")
            {
                // Extract the socket path from unix:///path/to/socket.sock
                var socketPath = endpoint.AbsolutePath;
                Log.Information("Creating HttpClient for Unix Domain Socket: {SocketPath}", socketPath);

                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    ConnectCallback = async (_, cancellationToken) =>
                    {
                        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                        var udsEndpoint = new UnixDomainSocketEndPoint(socketPath);
                        await socket.ConnectAsync(udsEndpoint, cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                };

                return new HttpClient(handler)
                {
                    DefaultRequestVersion = HttpVersion.Version20,
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
                };
            }

            // Standard TCP/IP endpoint
            var tcpHandler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            return new HttpClient(tcpHandler)
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
        }

        private async Task<bool> SendOtlpRequest(IReadOnlyList<MetricPoint> metrics)
        {
            try
            {
                // OTEL-style: serialize per protocol so we never leak the gRPC 5-byte reservation into HTTP.
                var task = _protocol switch
                {
                    Configuration.OtlpProtocol.Grpc =>
                        SendGrpcRequest(_serializer.SerializeMetrics(metrics, startPosition: 5)),
                    Configuration.OtlpProtocol.HttpProtobuf =>
                        SendHttpProtobufRequest(_serializer.SerializeMetrics(metrics, startPosition: 0)),
                    Configuration.OtlpProtocol.HttpJson =>
                        SendHttpJsonRequest(_serializer.SerializeMetrics(metrics, startPosition: 0)),
                    _ =>
                        SendHttpProtobufRequest(_serializer.SerializeMetrics(metrics, startPosition: 0))
                };

                return await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Seeing network connectivity errors so skipping telemetry
                Log.ErrorSkipTelemetry(ex, "Error sending OTLP request.");
                return false;
            }
        }

        private async Task<bool> SendHttpJsonRequest(byte[] otlpPayload)
        {
            Log.Warning("HTTP/JSON protocol is not yet implemented, falling back to HTTP/protobuf");
            return await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false);
        }

        private async Task<bool> SendHttpProtobufRequest(byte[] otlpPayload)
        {
            static HttpRequestMessage CreateHttpProtobufRequest(byte[] payload, Uri endpoint, IReadOnlyDictionary<string, string> headers)
            {
                var content = new ByteArrayContent(payload);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = content
                };

                foreach (var header in headers)
                {
                    httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                return httpRequest;
            }

            return await SendWithRetry(otlpPayload, CreateHttpProtobufRequest).ConfigureAwait(false);
        }

        private async Task<bool> SendGrpcRequest(byte[] otlpPayload)
        {
            if (_grpcClient is null)
            {
                Log.Warning("GRPC selected but gRPC client is not initialized; falling back to HTTP/protobuf.");
                // otlpPayload was serialized with startPosition=5 => slice off the 5-byte reservation.
                return await SendWithRetry(
                        otlpPayload,
                        (p, endpoint, headers) =>
                        {
                            var content = new ByteArrayContent(p, 5, p.Length - 5);
                            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

                            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                            {
                                Content = content
                            };

                            foreach (var header in headers)
                            {
                                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            }

                            return httpRequest;
                        })
                    .ConfigureAwait(false);
            }

            try
            {
                // gRPC requires a 5-byte message frame prefix:
                // byte 0: compression flag (0 = no compression)
                // bytes 1-4: message length in big-endian format
                // bytes 5+: the actual protobuf payload (already serialized at position 5)
                otlpPayload[0] = 0; // No compression

                // Write message length in big-endian format (payload length - 5 header bytes)
                var dataLength = otlpPayload.Length - 5;
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
                    new System.Span<byte>(otlpPayload, 1, 4),
                    (uint)dataLength);

                var resp = await Task.Run(() => _grpcClient.SendExportRequest(
                                            otlpPayload,
                                            otlpPayload.Length,
                                            default))
                                    .ConfigureAwait(false);
                return resp.Success;
            }
            catch (Exception ex)
            {
                // Seeing network connectivity errors so skipping telemetry
                Log.ErrorSkipTelemetry(ex, "Exception when sending metrics OTLP gRPC request.");
                return false;
            }
        }

        private async Task<bool> SendWithRetry(byte[] otlpPayload, HttpRequestFactory requestFactory)
        {
            const int maxRetries = 3;
            var retryDelay = TimeSpan.FromMilliseconds(100);

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using HttpRequestMessage httpRequest = requestFactory(otlpPayload, _endpoint, _headers);

                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_timeoutMs));
                    var response = await _httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
                        if (CheckForPartialSuccess(responseBody))
                        {
                            TelemetryFactory.Metrics.RecordCountMetricsExportPartialSuccesses(_protocolTag, _encodingTag);
                        }

                        return true;
                    }

                    var statusCode = (int)response.StatusCode;

                    if (statusCode == 400)
                    {
                        Log.Warning("Bad Request (400) - not retrying: {StatusCode}", response.StatusCode);
                        return false;
                    }

                    if (statusCode == 429 || statusCode == 502 || statusCode == 503 || statusCode == 504)
                    {
                        if (attempt < maxRetries)
                        {
                            var retryAfter = response.Headers.RetryAfter?.Delta;
                            if (retryAfter.HasValue)
                            {
                                retryDelay = retryAfter.Value;
                            }
                            else
                            {
                                retryDelay = TimeSpan.FromMilliseconds((long)(retryDelay.TotalMilliseconds * 2));
                            }

                            await Task.Delay(retryDelay).ConfigureAwait(false);
                            continue;
                        }
                    }

                    return false;
                }
                catch (TaskCanceledException) when (attempt < maxRetries)
                {
                    retryDelay = TimeSpan.FromMilliseconds((long)(retryDelay.TotalMilliseconds * 2));
                    await Task.Delay(retryDelay).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Debug<int>(ex, "Error sending OTLP request (attempt {Attempt})", attempt + 1);
                    if (attempt < maxRetries)
                    {
                        retryDelay = TimeSpan.FromMilliseconds((long)(retryDelay.TotalMilliseconds * 2));
                        await Task.Delay(retryDelay).ConfigureAwait(false);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the OTLP response indicates partial success by parsing the protobuf response body.
        /// According to OTLP spec, partial success is indicated by the presence of the partial_success field (field 1, wire type 2).
        /// </summary>
        private bool CheckForPartialSuccess(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
            {
                return false;
            }

            // ExportMetricsServiceResponse.partial_success has field number 1 (tag 0x0A = field 1, wire type 2)
            const byte partialSuccessTag = 0x0A;

            return payload[0] == partialSuccessTag;
        }
    }
}
#endif
