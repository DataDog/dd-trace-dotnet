// <copyright file="OpenTelemetryProtocolExporterEventSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.Tracing;
using Datadog.Trace.Logging;
#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
#endif
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Vendors.OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    /// <summary>
    /// Stub EventSource - we don't emit these events but the vendored code references it.
    /// The vendored gRPC client calls these methods for internal logging/telemetry,
    /// but we use our own logging infrastructure (Datadog.Trace.Logging) instead.
    /// </summary>
    internal sealed class OpenTelemetryProtocolExporterEventSource
    {
        private static readonly IDatadogLogger DatadogLogger = DatadogLogging.GetLoggerFor<OpenTelemetryProtocolExporterEventSource>();

        public static OpenTelemetryProtocolExporterEventSource Log { get; } = new OpenTelemetryProtocolExporterEventSource();

        public bool IsEnabled(EventLevel level, EventKeywords keywords) => true;

        public void FailedToReachCollector(Uri endpoint, Exception ex)
        {
            DatadogLogger.Error(ex, "OpenTelemetryProtocolExporterEventSource.FailedToReachCollector: Exporter failed send data to collector to {Endpoint} endpoint. Data will not be sent.", endpoint);
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

        public void ResponseDeserializationFailed(Uri endpoint)
        {
            DatadogLogger.Error<Uri>("OpenTelemetryProtocolExporterEventSource.ResponseDeserializationFailed: Failed to deserialize response from {Endpoint}.", endpoint);
        }

        public void ExportSuccess(Uri endpoint, string message)
        {
            if (DatadogLogger.IsEnabled(LogEventLevel.Debug))
            {
                DatadogLogger.Debug("OpenTelemetryProtocolExporterEventSource.ExportSuccess: Export succeeded for {Endpoint}. Message: {Message}.", endpoint, message);
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        public void ExportFailure(Uri endpoint, string message, Status status)
        {
            DatadogLogger.Error<Uri, string, Status>("OpenTelemetryProtocolExporterEventSource.ExportFailure: Export failed for {Endpoint}. Message: {Message}. Status: {Status}.", endpoint, message, status);
        }
#endif

        public void TransientHttpError(Uri endpoint, Exception ex)
        {
            DatadogLogger.Error(ex, "OpenTelemetryProtocolExporterEventSource.TransientHttpError: Transient HTTP error when communicating with {Endpoint}.", endpoint);
        }

        public void HttpRequestFailed(Uri endpoint, string? response, Exception ex)
        {
            DatadogLogger.Error<Uri, string>(ex, "OpenTelemetryProtocolExporterEventSource.ExportSuccess: HTTP request to {Endpoint} failed. Response: {Response}.", endpoint, response ?? "null");
        }

        public void OperationUnexpectedlyCanceled(Uri endpoint, Exception ex)
        {
            DatadogLogger.Error(ex, "OpenTelemetryProtocolExporterEventSource.OperationUnexpectedlyCanceled: Operation unexpectedly canceled for {Endpoint}.", endpoint);
        }

        public void RequestTimedOut(Uri endpoint, Exception ex)
        {
            DatadogLogger.Error(ex, "OpenTelemetryProtocolExporterEventSource.RequestTimedOut: Request to {Endpoint} time out.", endpoint);
        }

        public void GrpcRetryDelayParsingFailed(string? grpcStatusDetailsHeader, Exception ex)
        {
            DatadogLogger.Error<string>(ex, "OpenTelemetryProtocolExporterEventSource.GrpcRetryDelayParsingFailed: Failed to parse gRPC retry delay from grpcStatusDetailsHeader: {GrpcStatusDetailsHeader}.", grpcStatusDetailsHeader ?? "null");
        }

        public void BufferExceededMaxSize(string signalType, int bufferSize)
        {
            DatadogLogger.Error<string, int>("OpenTelemetryProtocolExporterEventSource.BufferExceededMaxSize: Buffer exceeded max size for {SignalType}. Buffer size: {BufferSize}.", signalType, bufferSize);
        }

        public void BufferResizeFailedDueToMemory(string signalType)
        {
            DatadogLogger.Error<string>("OpenTelemetryProtocolExporterEventSource.BufferResizeFailedDueToMemory: Buffer resize failed due to insufficient memory for {SignalType}.", signalType);
        }
    }
}
