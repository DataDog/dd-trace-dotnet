// <copyright file="OtlpExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
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

        _httpClient = CreateHttpClient();

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
    /// Shuts down the exporter and ensures all pending exports complete.
    /// </summary>
    /// <param name="timeoutMilliseconds">Maximum time to wait for shutdown</param>
    /// <returns>True if shutdown completed successfully</returns>
    public bool Shutdown(int timeoutMilliseconds)
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

    private HttpClient CreateHttpClient()
    {
#if NET6_0_OR_GREATER
        var tcpHandler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var httpClient = new HttpClient(tcpHandler)
        {
            Timeout = TimeSpan.FromMilliseconds(_timeoutMs),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };
#else
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(_timeoutMs),
            DefaultRequestVersion = HttpVersion.Version20,
        };
#endif

        foreach (var header in _headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        return httpClient;
    }

    private async Task<bool> SendOtlpRequest(IReadOnlyList<LogPoint> logs)
    {
        try
        {
            // Serialize logs to protobuf format using vendored OpenTelemetry protobuf utilities
            // For gRPC, reserve 5 bytes at the start for the frame header (added later)
            // For HTTP, start at position 0
            var startPosition = _protocol == OtlpProtocol.Grpc ? 5 : 0;
            var otlpPayload = OtlpLogsSerializer.SerializeLogs(logs, _resourceTags, startPosition);

            return _protocol switch
            {
                OtlpProtocol.HttpProtobuf => await SendHttpProtobufRequest(otlpPayload).ConfigureAwait(false),
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

    private Task<bool> SendHttpProtobufRequest(byte[] otlpPayload)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(_timeoutMs);
            var resp = _httpExportClient?.SendExportRequest(
                otlpPayload,
                otlpPayload.Length,
                deadline);

            return Task.FromResult(resp is { Success: true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception when sending logs OTLP HTTP request.");
            return Task.FromResult(false);
        }
    }

    private Task<bool> SendGrpcRequest(byte[] otlpPayload)
    {
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

            var deadline = DateTime.UtcNow.AddMilliseconds(_timeoutMs);
            var resp = _grpcClient?.SendExportRequest(
                otlpPayload,
                otlpPayload.Length,
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
