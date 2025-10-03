// <copyright file="OtlpExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

#nullable enable

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// OTLP Exporter implementation that exports metrics using the OpenTelemetry Protocol.
    /// This is the concrete implementation of the MetricExporter base class that handles OTLP protocol specifics.
    /// Supports both grpc and http/protobuf transports as specified in the RFC.
    /// This is a Push Metric Exporter that sends metrics via the OpenTelemetry Protocol.
    /// </summary>
    internal class OtlpExporter : MetricExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpExporter));
        private readonly HttpClient _httpClient;
        private readonly Telemetry.Metrics.MetricTags.Protocol _protocolTag;
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
                Configuration.OtlpProtocol.HttpProtobuf => Telemetry.Metrics.MetricTags.Protocol.HttpProtobuf,
                Configuration.OtlpProtocol.HttpJson => Telemetry.Metrics.MetricTags.Protocol.HttpJson,
                _ => Telemetry.Metrics.MetricTags.Protocol.HttpProtobuf
            };
            _serializer = new OtlpMetricsSerializer(settings);

            // Enable h2c (HTTP/2 without TLS) for gRPC only, and only for non-HTTPS endpoints
            if (_protocol == Configuration.OtlpProtocol.Grpc && _endpoint.Scheme != Uri.UriSchemeHttps)
            {
                Log.Information("Enabling HTTP/2 without TLS (h2c) support for gRPC endpoint: {Endpoint}", _endpoint);
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            _httpClient = CreateHttpClient(_endpoint);
        }

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

            TelemetryFactory.Metrics.RecordCountMetricsExportAttempts(_protocolTag);

            try
            {
                var success = await SendOtlpRequest(metrics).ConfigureAwait(false);

                if (success)
                {
                    TelemetryFactory.Metrics.RecordCountMetricsExportSuccesses(_protocolTag);
                    return ExportResult.Success;
                }
                else
                {
                    TelemetryFactory.Metrics.RecordCountMetricsExportFailures(_protocolTag);
                    return ExportResult.Failure;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting OTLP metrics.");
                TelemetryFactory.Metrics.RecordCountMetricsExportFailures(_protocolTag);
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
                    ConnectCallback = async (context, cancellationToken) =>
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

        /// <summary>
        /// Strips the 5-byte gRPC framing header (1 byte compressed flag + 4 bytes message length).
        /// Returns the original span if unframed/too short or if compression is detected (not handled).
        /// </summary>
        private static ReadOnlySpan<byte> TryUnframeGrpc(ReadOnlySpan<byte> body)
        {
            if (body.Length < 5)
            {
                return body;
            }

            var compressed = body[0] == 1;
            var len = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(1, 4));

            if (compressed || body.Length < 5 + len)
            {
                return body; // We don't handle compressed; return original
            }

            return body.Slice(5, (int)len);
        }

        private async Task<bool> SendOtlpRequest(IReadOnlyList<MetricPoint> metrics)
        {
            try
            {
                // Serialize metrics to protobuf format
                // Note: JSON serialization is not yet implemented, so we always use protobuf ATM
                var otlpPayload = _serializer.SerializeMetrics(metrics);

                return _protocol switch
                {
                    Configuration.OtlpProtocol.HttpProtobuf => await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false),
                    Configuration.OtlpProtocol.HttpJson => await SendHttpJsonRequest(otlpPayload).ConfigureAwait(false),
                    Configuration.OtlpProtocol.Grpc => await SendGrpcRequest(otlpPayload).ConfigureAwait(false),
                    _ => await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false)
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending OTLP request.");
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
            // For UDS, HttpRequestMessage needs http://localhost + path (the socket is chosen by ConnectCallback)
            // Config already includes /v1/metrics in the endpoint, we just need to rewrite the scheme/host
            var uri = _endpoint.Scheme == "unix"
                ? new Uri("http://localhost" + _endpoint.AbsolutePath)
                : _endpoint;

            using var content = new ByteArrayContent(otlpPayload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = content
            };

            foreach (var header in _headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return await SendWithRetry(httpRequest).ConfigureAwait(false);
        }

        private async Task<bool> SendGrpcRequest(byte[] otlpPayload)
        {
            Log.Warning("GRPC protocol is not yet implemented, falling back to HTTP/protobuf");
            return await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false);
        }

        private async Task<bool> SendWithRetry(HttpRequestMessage httpRequest)
        {
            const int maxRetries = 3;
            var retryDelay = TimeSpan.FromMilliseconds(100);

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_timeoutMs));
                    var response = await _httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
                        if (CheckForPartialSuccess(responseBody))
                        {
                            TelemetryFactory.Metrics.RecordCountMetricsExportPartialSuccesses(_protocolTag);
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
                        else
                        {
                            return false;
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
                    Log.Error(ex, "Error sending OTLP request (attempt {Attempt})", (attempt + 1).ToString());
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
        /// Expects unframed protobuf bytes (caller should use TryUnframeGrpc for gRPC responses).
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
