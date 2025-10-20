// <copyright file="OtlpExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Logs
{
    /// <summary>
    /// OTLP Exporter implementation that exports logs using the OpenTelemetry Protocol.
    /// Supports both gRPC and HTTP/Protobuf transports using vendored export clients.
    /// This is a Push Log Exporter that sends logs via the OpenTelemetry Protocol.
    /// </summary>
    internal class OtlpExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpExporter));
        private readonly HttpClient _httpClient;
        private readonly OtlpGrpcExportClient? _grpcClient;
        private readonly OtlpHttpExportClient? _httpExportClient;
        private readonly Uri _endpoint;
        private readonly IReadOnlyDictionary<string, string> _headers;
        private readonly int _timeoutMs;
        private readonly OtlpProtocol _protocol;
        private readonly TracerSettings _settings;

        public OtlpExporter(TracerSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _endpoint = settings.OtlpLogsEndpoint;
            _headers = settings.OtlpLogsHeaders;
            _timeoutMs = settings.OtlpLogsTimeoutMs;
            _protocol = settings.OtlpLogsProtocol;

            _httpClient = CreateHttpClient();

            if (_protocol == OtlpProtocol.Grpc)
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

                const string logsGrpcPath = "opentelemetry.proto.collector.logs.v1.LogsService/Export";
                _grpcClient = new OtlpGrpcExportClient(opt, _httpClient, logsGrpcPath);
            }
            else
            {
                var opt = new OtlpExporterOptions
                {
                    Endpoint = _endpoint,
                    TimeoutMilliseconds = _timeoutMs,
                    Protocol = (OtlpExportProtocol)_protocol,
                    AppendSignalPathToEndpoint = false // HTTP endpoint already includes /v1/logs
                };

                foreach (var kvp in _headers)
                {
                    opt.Headers[kvp.Key] = kvp.Value;
                }

                const string logsHttpPath = "v1/logs";
                _httpExportClient = new OtlpHttpExportClient(opt, _httpClient, logsHttpPath);
            }
        }

        /// <summary>
        /// Exports a batch of logs using OTLP protocol.
        /// This method implements the LogExporter base class Export operation.
        /// </summary>
        /// <param name="logs">Batch of logs to export</param>
        /// <returns>ExportResult indicating success or failure</returns>
        public ExportResult Export(IReadOnlyList<LogPoint> logs)
        {
            // For backward compatibility, call the async version synchronously
            return ExportAsync(logs).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Exports a batch of logs using OTLP protocol asynchronously.
        /// This is the preferred method for better performance.
        /// </summary>
        /// <param name="logs">Batch of logs to export</param>
        /// <returns>ExportResult indicating success or failure</returns>
        public async Task<ExportResult> ExportAsync(IReadOnlyList<LogPoint> logs)
        {
            if (logs.Count == 0)
            {
                Log.Debug("No logs to export.");
                return ExportResult.Success;
            }

            try
            {
                var success = await SendOtlpRequest(logs).ConfigureAwait(false);
                return success ? ExportResult.Success : ExportResult.Failure;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting OTLP logs.");
                return ExportResult.Failure;
            }
        }

        /// <summary>
        /// Shuts down the exporter and ensures all pending exports complete.
        /// </summary>
        /// <param name="timeoutMilliseconds">Maximum time to wait for shutdown</param>
        /// <returns>True if shutdown completed successfully</returns>
        public bool Shutdown(int timeoutMilliseconds)
        {
            try
            {
                _httpClient.CancelPendingRequests();
                _httpClient.Dispose();
                Log.Debug("OTLP Log Exporter shutdown completed.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during OTLP Log Exporter shutdown.");
                return false;
            }
        }

        private static HttpClient CreateHttpClient()
        {
#if NET6_0_OR_GREATER
            var tcpHandler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            return new HttpClient(tcpHandler)
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
#else
            return new HttpClient
            {
                DefaultRequestVersion = HttpVersion.Version20,
            };
#endif
        }

        private async Task<bool> SendOtlpRequest(IReadOnlyList<LogPoint> logs)
        {
            try
            {
                // Serialize logs to protobuf format using vendored OpenTelemetry protobuf utilities
                // For gRPC, reserve 5 bytes at the start for the frame header (added later)
                // For HTTP, start at position 0
                var startPosition = _protocol == OtlpProtocol.Grpc ? 5 : 0;
                var otlpPayload = OtlpLogsSerializer.SerializeLogs(logs, _settings, startPosition);

                return _protocol switch
                {
                    OtlpProtocol.HttpProtobuf => await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false),
                    OtlpProtocol.HttpJson => await SendHttpJsonRequest(otlpPayload).ConfigureAwait(false),
                    OtlpProtocol.Grpc => await SendGrpcRequest(otlpPayload).ConfigureAwait(false),
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
            Log.Warning("HTTP/JSON protocol is not yet implemented for logs, falling back to HTTP/protobuf");
            return await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false);
        }

        private async Task<bool> SendHttpProtobufRequest(byte[] otlpPayload)
        {
            if (_httpExportClient is null)
            {
                Log.Warning("HTTP/Protobuf selected but HTTP client is not initialized; cannot send logs.");
                return false;
            }

            try
            {
                var resp = await Task.Run(() => _httpExportClient.SendExportRequest(
                                            otlpPayload,
                                            otlpPayload.Length,
                                            default))
                                    .ConfigureAwait(false);
                return resp.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception when sending logs OTLP HTTP request.");
                return false;
            }
        }

        private async Task<bool> SendGrpcRequest(byte[] otlpPayload)
        {
            if (_grpcClient is null)
            {
                Log.Warning("GRPC selected but gRPC client is not initialized; falling back to HTTP/protobuf.");
                return await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false);
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
                Log.Error(ex, "Exception when sending logs OTLP gRPC request.");
                return false;
            }
        }
    }
}

#endif
