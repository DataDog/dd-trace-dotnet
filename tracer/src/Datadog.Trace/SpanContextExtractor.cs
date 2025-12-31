// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The <see cref="SpanContextExtractor"/> is responsible for extracting <see cref="ISpanContext"/> in the rare cases
    /// where the Tracer couldn't propagate it itself. This can happen for instance when libraries add an extra
    /// layer above the instrumented ones (eg consuming Kafka messages and enqueuing them prior to generate a span).
    /// When messageType and target are specified, also used to set data streams monitoring checkpoints (if enabled).
    /// </summary>
    public static class SpanContextExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanContextExtractor));

        internal static SpanContext? Extract<TCarrier>(Tracer tracer, TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string? messageType = null, string? source = null)
        {
            if (messageType != null && source == null) { ThrowHelper.ThrowArgumentNullException(nameof(source)); }
            else if (messageType == null && source != null) { ThrowHelper.ThrowArgumentNullException(nameof(messageType)); }

            var context = tracer.TracerManager.SpanContextPropagator.Extract(carrier, getter);

            // DSM
            if (context.SpanContext is { } spanContext
                && tracer.TracerManager.DataStreamsManager is { IsEnabled: true } dsm)
            {
                if (getter(carrier, DataStreamsPropagationHeaders.TemporaryBase64PathwayContext).FirstOrDefault() is { Length: > 0 } base64PathwayContext)
                {
                    // Kafka special: check if there is a pathway context to recover from the message. If so, just set the pathway on the span context so that it'll be picked by child spans.
                    // This allows users who consume in batch to recover the correct pathway by calling this method before producing a new message downstream from this one.
                    // (otherwise, only the pathway of the last consumed message would be used)
                    var currentPathwayContext = TryGetPathwayContext(base64PathwayContext);
                    spanContext.ManuallySetPathwayContextToPairMessages(currentPathwayContext);
                } // otherwise, set a normal checkpoint if parameters are provided
                else if (!string.IsNullOrEmpty(messageType) && !string.IsNullOrEmpty(source))
                {
                    var parentPathwayContext = dsm.ExtractPathwayContextAsBase64String(new CarrierWithDelegate<TCarrier>(carrier, getter));
                    var edgeTags = new[] { "direction:in", $"topic:{source}", $"type:{messageType}" };
                    spanContext.SetCheckpoint(dsm, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0, parentPathwayContext);
                }
            }

            return context.SpanContext;
        }

        private static PathwayContext? TryGetPathwayContext(string? base64PathwayContext)
        {
            if (string.IsNullOrEmpty(base64PathwayContext))
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64PathwayContext);
                return PathwayContextEncoder.Decode(bytes);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting pathway context from base64 encoded pathway {Base64PathwayContext}", base64PathwayContext);
            }

            return null;
        }
    }
}
