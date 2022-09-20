// <copyright file="TraceChunkModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Contains one of more spans (a trace chunk) that belong to the same trace
/// and additional context needed for serialization.
/// </summary>
internal readonly struct TraceChunkModel
{
    public readonly ArraySegment<Span> Spans;

    public readonly Span? LocalRoot;

    public readonly int? SamplingPriority;

    public readonly TraceTagCollection? Tags;

    public TraceChunkModel(in ArraySegment<Span> spans, TraceContext? traceContext)
        : this(spans, traceContext?.RootSpan, traceContext?.SamplingPriority, traceContext?.Tags)
    {
    }

    public TraceChunkModel(
        in ArraySegment<Span> spans,
        Span? localRoot,
        int? samplingPriority,
        TraceTagCollection? tags)
    {
        Spans = spans;
        LocalRoot = localRoot;
        SamplingPriority = samplingPriority;
        Tags = tags;
    }
}
