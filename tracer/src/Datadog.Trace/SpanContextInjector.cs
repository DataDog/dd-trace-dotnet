// <copyright file="SpanContextInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContextInjector is responsible for injecting SpanContext in the rare cases where the Tracer couldn't propagate it itself.
    /// This can happen for instance when we don't support a specific library
    /// </summary>
    public class SpanContextInjector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextInjector"/> class
        /// </summary>
        [PublicApi]
        public SpanContextInjector()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Ctor);
        }

        /// <summary>
        /// Given a SpanContext carrier and a function to set a value, this method will inject a SpanContext.
        /// You should only call <see cref="Inject{TCarrier}"/> once on the message <paramref name="carrier"/>. Calling
        /// multiple times may lead to incorrect behaviors.
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="setter">Given a key name and value, sets the value in the carrier</param>
        /// <param name="context">The context you want to inject</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        [PublicApi]
        public void Inject<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Inject);
            InjectInternal(carrier, setter, context);
        }

        /// <summary>
        /// Given a SpanContext carrier and a function to set a value, this method will inject a SpanContext.
        /// You should only call <see cref="Inject{TCarrier}"/> once on the message <paramref name="carrier"/>. Calling
        /// multiple times may lead to incorrect behaviors.
        /// This method also sets a data streams monitoring checkpoint (if enabled).
        /// </summary>
        /// <param name="carrier">The carrier of the SpanContext. Often a header (http, kafka message header...)</param>
        /// <param name="setter">Given a key name and value, sets the value in the carrier</param>
        /// <param name="context">The context you want to inject</param>
        /// <param name="messageType">For Data Streams Monitoring: The type of messaging system where the data being injected will be sent.</param>
        /// <param name="target">For Data Streams Monitoring: The queue or topic where the data being injected will be sent.</param>
        /// <typeparam name="TCarrier">Type of the carrier</typeparam>
        [PublicApi]
        public void InjectIncludingDsm<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context, string messageType, string target)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_InjectIncludingDsm);
            InjectInternal(carrier, setter, context, messageType, target);
        }

        internal static void InjectInternal<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext context, string? messageType = null, string? target = null)
        {
            if (messageType != null && target == null) { ThrowHelper.ThrowArgumentNullException(nameof(target)); }
            else if (messageType == null && target != null) { ThrowHelper.ThrowArgumentNullException(nameof(messageType)); }

            if (context == null!) { ThrowHelper.ThrowArgumentNullException(nameof(context)); }

            if (context is SpanContext spanContext)
            {
                SpanContextPropagator.Instance.Inject(spanContext, carrier, setter);

                if (string.IsNullOrEmpty(messageType) || string.IsNullOrEmpty(target))
                {
                    return;
                }

                var dsm = Tracer.Instance.TracerManager.DataStreamsManager;
                if (dsm != null && dsm.IsEnabled)
                {
                    var edgeTags = new[] { "direction:out", $"topic:{target}", $"type:{messageType}" };
                    spanContext.SetCheckpoint(dsm, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0, parent: null);
                    if (carrier != null)
                    {
                        dsm.InjectPathwayContextAsBase64String(spanContext.PathwayContext, new CarrierWithDelegate<TCarrier>(carrier, setter: setter));
                    }
                }
            }
        }
    }
}
