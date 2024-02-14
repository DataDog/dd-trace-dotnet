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
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

#nullable enable

namespace Datadog.Trace
{
    /// <inheritdoc />
    public class SpanContextExtractor : ISpanContextExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanContextExtractor>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextExtractor"/> class
        /// </summary>
        [PublicApi]
        public SpanContextExtractor()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextExtractor_Ctor);
        }

        internal SpanContextExtractor(bool unusedParamNotToUsePublicApi)
        {
            // unused parameter is to give us a non-public API we can use
        }

        /// <inheritdoc />
        [PublicApi]
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextExtractor_Extract);
            return ExtractInternal(carrier, getter);
        }

        /// <inheritdoc />
        [PublicApi]
        public ISpanContext? ExtractIncludingDsm<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string messageType, string source)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextExtractor_ExtractIncludingDsm);
            return ExtractInternal(carrier, getter, messageType, source);
        }

        internal static SpanContext? ExtractInternal<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string? messageType = null, string? source = null)
        {
            var spanContext = SpanContextPropagator.Instance.Extract(carrier, getter);

            if (spanContext is not null
             && Tracer.Instance.TracerManager.DataStreamsManager is { IsEnabled: true } dsm)
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

            return spanContext;
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
