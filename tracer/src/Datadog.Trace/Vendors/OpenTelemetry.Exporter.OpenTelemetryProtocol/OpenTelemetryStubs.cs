// <copyright file="OpenTelemetryStubs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1649 // File name should match first type name
#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;

namespace OpenTelemetry.Internal
{
    internal static class Guard
    {
        public static void ThrowIfNull(object? value, string? paramName = null)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }

    internal static class UriExtensions
    {
        public static Uri AppendPathIfNotPresent(this Uri uri, string path)
        {
            var absoluteUri = uri.AbsoluteUri;
            if (absoluteUri.EndsWith(path, StringComparison.Ordinal))
            {
                return uri;
            }

            return new Uri(uri, path);
        }
    }
}

namespace Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// OtlpExportProtocol enum from OpenTelemetry.Exporter.OpenTelemetryProtocol.
    /// We keep this in our stub because the original file is in the parent namespace
    /// OpenTelemetry.Exporter which would require additional namespace rewriting rules.
    /// Values match OpenTelemetry's enum (Grpc=0, HttpProtobuf=1).
    /// </summary>
    internal enum OtlpExportProtocol : byte
    {
        Grpc = 0,
        HttpProtobuf = 1,
    }

    /// <summary>
    /// Minimal OtlpExporterOptions needed by the vendored gRPC export client.
    /// This avoids vendoring the full OpenTelemetry SDK types.
    /// </summary>
    internal sealed class OtlpExporterOptions
    {
        public Uri Endpoint { get; set; } = new("http://localhost:4317");

        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int TimeoutMilliseconds { get; set; } = 10000;

        public OtlpExportProtocol Protocol { get; set; } = OtlpExportProtocol.Grpc;

        public bool AppendSignalPathToEndpoint { get; set; } = true;

        public T GetHeaders<T>(Action<T, string, string> addHeader)
            where T : new()
        {
            var result = new T();
            foreach (var kvp in Headers)
            {
                addHeader(result, kvp.Key, kvp.Value);
            }

            return result;
        }
    }
}

namespace Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    /// <summary>
    /// Stub EventSource - we don't emit these events but the vendored code references it.
    /// </summary>
    internal sealed class OpenTelemetryProtocolExporterEventSource
    {
        public static OpenTelemetryProtocolExporterEventSource Log { get; } = new OpenTelemetryProtocolExporterEventSource();

        public void FailedToReachCollector(Uri endpoint, Exception ex)
        {
        }

        public void ExportMethodException(Exception ex, bool isRetry = false)
        {
        }

        public void ReceivedRpcRetryDelay(TimeSpan delay)
        {
        }

        public void ReceivedRpcRetryDelayHasExpiredDeadlineWasReached(TimeSpan delay, TimeSpan deadline)
        {
        }

        public void RetryDelayCancellationRequested()
        {
        }

        public void RetryDelayException(Exception ex)
        {
        }

        public void CouldNotReadGrpcStatusDetails(string grpcStatusDetailsHeader)
        {
        }

        public void ResponseDeserializationFailed(params object?[]? args)
        {
        }

        public void ExportSuccess(params object?[]? args)
        {
        }

        public void ExportFailure(params object?[]? args)
        {
        }

        public void TransientHttpError(params object?[]? args)
        {
        }

        public void HttpRequestFailed(params object?[]? args)
        {
        }

        public void OperationUnexpectedlyCanceled(params object?[]? args)
        {
        }

        public void RequestTimedOut(params object?[]? args)
        {
        }

        public void GrpcRetryDelayParsingFailed(params object?[]? args)
        {
        }
    }
}

#endif
