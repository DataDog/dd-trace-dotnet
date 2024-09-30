// <copyright file="PropagationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Propagators;

internal readonly struct PropagationContext
{
    public PropagationContext(SpanContext? spanContext, Baggage? baggage)
    {
        SpanContext = spanContext;
        Baggage = baggage;
    }

    public SpanContext? SpanContext { get; }

    public Baggage? Baggage { get; }

    public bool IsEmpty => SpanContext is null && Baggage is null;
}
