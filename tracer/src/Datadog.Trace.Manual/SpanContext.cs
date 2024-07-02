// <copyright file="SpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

/// <summary>
/// The SpanContext contains all the information needed to express relationships between spans inside or outside the process boundaries.
/// </summary>
public sealed class SpanContext : ISpanContext
{
    /// <summary>
    /// An <see cref="ISpanContext"/> with default values. Can be used as the value for
    /// <see cref="SpanCreationSettings.Parent"/> in <see cref="Tracer.StartActive(string, SpanCreationSettings)"/>
    /// to specify that the new span should not inherit the currently active scope as its parent.
    /// </summary>
    public static readonly ISpanContext None = new ReadOnlySpanContext(traceIdLower: 0, traceIdUpper: 0, spanId: 0, serviceName: null);

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanContext"/> class
    /// from a propagated context. <see cref="Parent"/> will be null
    /// since this is a root context locally.
    /// </summary>
    /// <param name="traceId">The propagated trace id.</param>
    /// <param name="spanId">The propagated span id.</param>
    /// <param name="samplingPriority">The propagated sampling priority.</param>
    /// <param name="serviceName">The service name to propagate to child spans.</param>
    public SpanContext(ulong? traceId, ulong spanId, SamplingPriority? samplingPriority = null, string? serviceName = null)
        : this(traceIdLower: traceId, traceIdUpper: 0, spanId: spanId, serviceName)
    {
        // Save this so we can use it for manual injection later if required
        SamplingPriority = (int?)samplingPriority;
    }

    internal SpanContext(ulong? traceIdLower, ulong? traceIdUpper, ulong spanId, string? serviceName = null)
    {
        TraceId = traceIdLower ?? 0;
        TraceIdUpper = traceIdUpper ?? 0;
        SpanId = spanId;
        ServiceName = serviceName;
        Parent = null;
    }

    /// <summary>
    /// Gets the parent context. This will always be null as <see cref="SpanContext"/> represents a root context;
    /// </summary>
    public ISpanContext? Parent { get; }

    /// <summary>
    /// Gets the 64-bit trace id, or the lower 64 bits of a 128-bit trace id.
    /// </summary>
    [DuckTypeTarget]
    public ulong TraceId { get; }

    /// <summary>
    /// Gets the upper 64 bits of a 128-bit trace id (or 0 if using 64-bit trace IDs).
    /// </summary>
    [DuckTypeTarget]
    internal ulong TraceIdUpper { get; }

    /// <summary>
    /// Gets the span id of the parent span. This will always be null as <see cref="SpanContext"/> represents a root context;
    /// </summary>
    public ulong? ParentId => Parent?.SpanId;

    /// <summary>
    /// Gets the span id.
    /// </summary>
    [DuckTypeTarget]
    public ulong SpanId { get; }

    /// <summary>
    /// Gets or sets the service name to propagate to child spans.
    /// </summary>
    [DuckTypeTarget]
    public string? ServiceName { get; set; }

    [DuckTypeTarget]
    internal int? SamplingPriority { get; }
}
