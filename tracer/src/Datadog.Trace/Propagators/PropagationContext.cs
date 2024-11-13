// <copyright file="PropagationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.OpenTelemetry.Baggage;

namespace Datadog.Trace.Propagators;

internal readonly struct PropagationContext
{
    public readonly SpanContext? SpanContext;

    public readonly Baggage? Baggage;

    public PropagationContext(SpanContext? spanContext, Baggage? baggage)
    {
        SpanContext = spanContext;
        Baggage = baggage;
    }

    public bool IsEmpty => SpanContext is null && Baggage is null;

    public static PropagationContext CreateContext(
        SpanContext? spanContext = null,
        Baggage? baggage = null,
        IReadOnlyDictionary<string, string>? additionalBaggageItems = null)
    {
        // use default values if not provided
        spanContext ??= Tracer.Instance.InternalActiveScope?.Span.Context;
        baggage ??= Baggage.Current;
        additionalBaggageItems ??= OpenTelemetryBaggage.GetBaggageItems();

        if (additionalBaggageItems?.Count > 0)
        {
            var combinedBaggage = new Baggage(baggage.Count + additionalBaggageItems.Count);
            baggage.MergeInto(combinedBaggage);
            baggage.AddOrReplace(additionalBaggageItems);

            return new PropagationContext(spanContext, combinedBaggage);
        }

        return new PropagationContext(spanContext, baggage);
    }
}
