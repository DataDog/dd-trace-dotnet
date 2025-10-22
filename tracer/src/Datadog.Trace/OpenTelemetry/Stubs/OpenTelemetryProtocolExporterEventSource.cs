// <copyright file="OpenTelemetryProtocolExporterEventSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System;

namespace Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    /// <summary>
    /// Stub EventSource - we don't emit these events but the vendored code references it.
    /// The vendored gRPC client calls these methods for internal logging/telemetry,
    /// but we use our own logging infrastructure (Datadog.Trace.Logging) instead.
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

        public void BufferExceededMaxSize(string signalType, int bufferSize)
        {
        }

        public void BufferResizeFailedDueToMemory(string signalType)
        {
        }
    }
}
#endif
