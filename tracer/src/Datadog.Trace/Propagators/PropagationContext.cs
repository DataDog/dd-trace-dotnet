// <copyright file="PropagationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Propagators;

internal readonly struct PropagationContext
{
    public readonly SpanContext? SpanContext;

    public readonly Baggage? Baggage;

    public readonly IEnumerable<SpanLink>? Links;

    public PropagationContext(SpanContext? spanContext, Baggage? baggage)
    {
        SpanContext = spanContext;
        Baggage = baggage;
        Links = null;
    }

    public PropagationContext(SpanContext? spanContext, Baggage? baggage, IEnumerable<SpanLink>? extractionSpanLinks)
    {
        SpanContext = spanContext;
        Baggage = baggage;
        Links = extractionSpanLinks;
    }

    public bool IsEmpty => SpanContext is null && Baggage is null;
}
