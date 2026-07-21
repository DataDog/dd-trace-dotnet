// <copyright file="OtlpExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol;
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Logs;

/// <summary>
/// OTLP Exporter implementation that exports logs using the OpenTelemetry Protocol.
/// Supports both gRPC and HTTP/Protobuf transports using vendored export clients.
/// This is a Push Log Exporter that sends logs via the OpenTelemetry Protocol.
/// </summary>
internal sealed class OtlpExporter : IOtlpExporter
{
    // Initial size of the buffer rented per export; grows on demand for larger batches.
    private const int InitialBufferSize = 64 * 1024;

    // Upper bound for a serialized batch. The buffer grows up to this cap; a batch that still
    // doesn't fit is dropped rather than allocating without bound. Matches the 3MB payload cap
    // used by our own direct log submission (DirectSubmissionLogSink.MaxTotalSizeBytes).
    private const int MaxBufferSize = 3 * 1024 * 1024;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpExporter));
    private readonly HttpClient _httpClient;
    private readonly OtlpGrpcExportClient? _grpcClient;
    private readonly OtlpHttpExportClient? _httpExportClient;
    private readonly IReadOnlyDictionary<string, string> _headers;
    private readonly int _timeoutMs;
    private readonly OtlpProtocol _protocol;
    private OtlpLogsSerializer.ResourceTags _resourceTags;

    public OtlpExporter(TracerSettings settings)
    {
        var endpoint = settings.OtlpLogsEndpoint;
        _headers = settings.OtlpLogsHeaders;
        _timeoutMs = settings.OtlpLogsTimeoutMs;
        _protocol = settings.OtlpLogsProtocol;
        UpdateResourceTags(settings.Manager.InitialMutableSettings);
        settings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedMutable is { } mutable)
            {
                UpdateResourceTags(mutable);
            }
        });

        _httpClient = CreateHttpClient(_timeoutMs, _headers);

        if (_protocol == OtlpProtocol.Grpc)
        {
            var opt = new OtlpExporterOptions
            {
                Endpoint = endpoint,
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
                Endpoint = endpoint,
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

        [MemberNotNull(nameof(_resourceTags))]
        void UpdateResourceTags(MutableSettings mutable)
        {
            var newTags = new OtlpLogsSerializer.ResourceTags(
                serviceName: mutable.DefaultServiceName,
                environment: mutable.Environment,
                serviceVersion: mutable.ServiceVersion,
                globalTags: mutable.GlobalTags);
            Interlocked.Exchange(ref _resourceTags, newTags);
        }
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
    /// Releases the exporter's HTTP resources. The final flush already ran
    /// synchronously in the sink's DisposeAsync (bounded by the HTTP client
    /// timeout, OTEL_EXPORTER_OTLP_TIMEOUT), so there is nothing further to wait on.
    /// </summary>
    /// <returns>True if shutdown completed successfully</returns>
    public bool Shutdown()
    {
        try
        {
            _httpClient.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during OTLP Log Exporter shutdown.");
            return false;
        }
    }

    internal static HttpClient CreateHttpClient(int timeoutMs, IReadOnlyDictionary<string, string> headers)
    {
#if NET6_0_OR_GREATER
        var tcpHandler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var httpClient = new HttpClient(tcpHandler)
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
        };
#else
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
        };
#endif

        foreach (var header in headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");

        return httpClient;
    }

    private async Task<bool> SendOtlpRequest(IReadOnlyList<LogPoint> logs)
    {
        // For gRPC, reserve 5 bytes at the start for the frame header (added later).
        // For HTTP, start at position 0.
        var startPosition = _protocol == OtlpProtocol.Grpc ? 5 : 0;

        // Rent a buffer to serialize into, growing it (doubling, up to MaxBufferSize) if the batch
        // doesn't fit. The send path consumes the buffer synchronously, so it is safe to return it
        // to the pool once the request completes.
        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        try
        {
            int bytesWritten;
            while (!OtlpLogsSerializer.TrySerializeLogs(logs, buffer, _resourceTags, out bytesWritten, startPosition))
            {
                if (buffer.Length >= MaxBufferSize)
                {
                    // The batch is too large to serialize and cannot be sent. Drop it (retrying
                    // would just overflow again) but report success so the batching sink doesn't
                    // trip its circuit breaker for a non-transient issue.
                    Log.Warning<int>("Dropping OTLP log batch of {Count} logs: serialized payload exceeds the maximum size.", logs.Count);
                    return true;
                }

                // Rent the larger buffer before returning the old one so a failed rent can't leave
                // us returning the same array twice via the finally block.
                var newBuffer = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length * 2, MaxBufferSize));
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = newBuffer;
            }

            return _protocol switch
            {
                OtlpProtocol.HttpProtobuf => await SendHttpProtobufRequest(buffer, bytesWritten).ConfigureAwait(false),
                OtlpProtocol.Grpc => await SendGrpcRequest(buffer, bytesWritten).ConfigureAwait(false),
                _ => await SendHttpProtobufRequest(buffer, bytesWritten).ConfigureAwait(false)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending OTLP request.");
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private Task<bool> SendHttpProtobufRequest(byte[] otlpPayload, int contentLength)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(_timeoutMs);
            var resp = _httpExportClient?.SendExportRequest(
                otlpPayload,
                contentLength,
                deadline);

            return Task.FromResult(resp is { Success: true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception when sending logs OTLP HTTP request.");
            return Task.FromResult(false);
        }
    }

    private Task<bool> SendGrpcRequest(byte[] otlpPayload, int contentLength)
    {
        try
        {
            // gRPC requires a 5-byte message frame prefix:
            // byte 0: compression flag (0 = no compression)
            // bytes 1-4: message length in big-endian format
            // bytes 5+: the actual protobuf payload (already serialized at position 5)
            otlpPayload[0] = 0; // No compression

            // Write message length in big-endian format (content length - 5 header bytes)
            var dataLength = contentLength - 5;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
                new System.Span<byte>(otlpPayload, 1, 4),
                (uint)dataLength);

            var deadline = DateTime.UtcNow.AddMilliseconds(_timeoutMs);
            var resp = _grpcClient?.SendExportRequest(
                otlpPayload,
                contentLength,
                deadline);
            return Task.FromResult(resp is { Success: true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception when sending logs OTLP gRPC request.");
            return Task.FromResult(false);
        }
    }
}
#endif
