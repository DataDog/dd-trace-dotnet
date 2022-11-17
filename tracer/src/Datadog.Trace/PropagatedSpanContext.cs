// <copyright file="PropagatedSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace;

internal class PropagatedSpanContext : IPropagatedSpanContext
{
    public PropagatedSpanContext(
        ulong traceId,
        ulong spanId,
        string? rawTraceId,
        string? rawSpanId,
        int? samplingPriority,
        string? origin,
        string? propagatedTags)
    {
        TraceId = traceId;
        SpanId = spanId;
        RawTraceId = rawTraceId;
        RawSpanId = rawSpanId;
        SamplingPriority = samplingPriority;
        Origin = origin;
        PropagatedTags = propagatedTags;
    }

    public ulong TraceId { get; }

    public ulong SpanId { get; }

    public string? RawTraceId { get; }

    public string? RawSpanId { get; }

    public int? SamplingPriority { get; }

    public string? Origin { get; }

    public string? PropagatedTags { get; }

    /// <summary>
    /// Gets <c>null</c>. Not used.
    /// </summary>
    /// <returns><c>null</c></returns>
    string? ISpanContext.ServiceName => null;
}
