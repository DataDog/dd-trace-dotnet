// <copyright file="ReadOnlySpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

internal sealed class ReadOnlySpanContext : ISpanContext
{
    public ReadOnlySpanContext(ulong traceIdLower, ulong traceIdUpper, ulong spanId, string? serviceName)
    {
        TraceId = traceIdLower;
        TraceIdUpper = traceIdUpper;
        SpanId = spanId;
        ServiceName = serviceName;
    }

    /// <summary>
    /// Gets the lower 64 bits of the 128-bit trace identifier.
    /// </summary>
    [DuckTypeTarget]
    public ulong TraceId { get; }

    /// <summary>
    /// Gets the upper 128-bit trace identifier.
    /// </summary>
    [DuckTypeTarget]
    internal ulong TraceIdUpper { get; }

    /// <summary>
    /// Gets the 64-bit span identifier.
    /// </summary>
    [DuckTypeTarget]
    public ulong SpanId { get; }

    /// <summary>
    /// Gets the service name to propagate to child spans.
    /// </summary>
    [DuckTypeTarget]
    public string? ServiceName { get; }
}
