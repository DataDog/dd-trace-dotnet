// <copyright file="SpanContextInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContextInjector is responsible for injecting SpanContext in the rare cases where the Tracer couldn't propagate it itself.
    /// This can happen for instance when we don't support a specific library
    /// </summary>
    public static class SpanContextInjector
    {
        internal static void Inject<TCarrier>(Tracer tracer, TCarrier carrier, Action<TCarrier, string, string> setter, ISpanContext? context, string? messageType = null, string? target = null)
        {
            if (messageType != null && target == null) { ThrowHelper.ThrowArgumentNullException(nameof(target)); }
            else if (messageType == null && target != null) { ThrowHelper.ThrowArgumentNullException(nameof(messageType)); }

            if (context is not SpanContext spanContext)
            {
                return;
            }

            tracer.TracerManager.SpanContextPropagator.Inject(
                new PropagationContext(spanContext, baggage: null),
                carrier,
                setter);

            // DSM
            if (!string.IsNullOrEmpty(messageType) &&
                !string.IsNullOrEmpty(target) &&
                tracer.TracerManager.DataStreamsManager is { IsEnabled: true } dsm)
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
