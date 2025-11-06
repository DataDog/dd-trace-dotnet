// <copyright file="OtlpLogEventBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.OpenTelemetry.Logs;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting
{
    /// <summary>
    /// Helper class to build OTLP log events from ILogger log calls.
    /// Extracts structured data and trace context for OTLP export.
    /// </summary>
    internal static class OtlpLogEventBuilder
    {
        /// <summary>
        /// Creates a LoggerDirectSubmissionLogEvent with OTLP structured data from ILogger state.
        /// </summary>
        public static LoggerDirectSubmissionLogEvent CreateLogEvent<TState>(
            int logLevel,
            string categoryName,
            object eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var attributes = ExtractAttributes(state, eventId);
            var (traceId, spanId, flags) = ExtractTraceContext();

            return new LoggerDirectSubmissionLogEvent(null)
            {
                OtlpLog = new LogPoint
                {
                    Message = message,
                    LogLevel = logLevel,
                    CategoryName = categoryName,
                    Timestamp = DateTimeOffset.UtcNow.DateTime,
                    Exception = exception,
                    Attributes = attributes,
                    TraceId = traceId,
                    SpanId = spanId,
                    Flags = flags
                }
            };
        }

        private static Dictionary<string, object?> ExtractAttributes<TState>(TState state, object eventId)
        {
            var attributes = new Dictionary<string, object?>();

            // Extract structured properties from ILogger state
            if (state is IReadOnlyList<KeyValuePair<string, object?>> properties)
            {
                foreach (var property in properties)
                {
                    if (!string.IsNullOrEmpty(property.Key) && property.Key != "{OriginalFormat}")
                    {
                        attributes[property.Key] = property.Value;
                    }
                }
            }

            // Add EventId if present
            if (eventId.GetHashCode() != 0)
            {
                attributes["EventId"] = eventId.GetHashCode();
            }

            return attributes;
        }

        private static (TraceId TraceId, ulong SpanId, int Flags) ExtractTraceContext()
        {
            // Prefer Datadog's trace context for accurate correlation with DD spans
            // Fallback to Activity if Datadog tracing is not active
            var ddSpan = Tracer.Instance.ActiveScope?.Span as Span;
            if (ddSpan != null)
            {
                var flags = ddSpan.Context.SamplingPriority is { } samplingPriority && SamplingPriorityValues.IsKeep(samplingPriority) ? 1 : 0;
                return (ddSpan.TraceId128, ddSpan.SpanId, flags);
            }

            var activity = System.Diagnostics.Activity.Current;
            if (activity != null && activity.IdFormat == System.Diagnostics.ActivityIdFormat.W3C)
            {
                if (HexString.TryParseTraceId(activity.TraceId.ToHexString(), out var activityTraceId) &&
                    HexString.TryParseUInt64(activity.SpanId.ToHexString(), out var activitySpanId))
                {
                    var flags = activity.Recorded ? 1 : 0;
                    return (activityTraceId, activitySpanId, flags);
                }
            }

            return (TraceId.Zero, 0, 0);
        }
    }
}

#endif

