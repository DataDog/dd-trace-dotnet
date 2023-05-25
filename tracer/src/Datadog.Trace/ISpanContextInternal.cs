// <copyright file="ISpanContextInternal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Tagging;

namespace Datadog.Trace;

internal interface ISpanContextInternal : ISpanContext, IReadOnlyDictionary<string, string>
{
    /// <summary>
    /// Gets the parent context.
    /// </summary>
    ISpanContext Parent { get; }

    /// <summary>
    /// Gets the 128-bit trace id.
    /// </summary>
    TraceId TraceId128 { get; }

    /// <summary>
    /// Gets the span id of the parent span.
    /// </summary>
    ulong? ParentId { get; }

    /// <summary>
    /// Gets or sets the origin of the trace.
    /// For local contexts, this property delegates to TraceContext.Origin.
    /// This is a temporary work around because we use SpanContext
    /// for all local spans and also for propagation.
    /// </summary>
    string Origin { get; set; }

    /// <summary>
    /// Gets or sets the propagated trace tags collection.
    /// </summary>
    TraceTagCollection PropagatedTags { get; set; }

    /// <summary>
    /// Gets the trace context.
    /// Returns null for contexts created from incoming propagated context.
    /// </summary>
    TraceContext TraceContext { get; }

    /// <summary>
    /// Gets the sampling priority for contexts created from incoming propagated context.
    /// Returns null for local contexts.
    /// </summary>
    int? SamplingPriority { get; }

    /// <summary>
    /// Gets the trace id as a hexadecimal string of length 32,
    /// padded with zeros to the left if needed.
    /// </summary>
    string RawTraceId { get; }

    /// <summary>
    /// Gets the span id as a hexadecimal string of length 16,
    /// padded with zeros to the left if needed.
    /// </summary>
    string RawSpanId { get; }

    /// <summary>
    /// Gets or sets additional key/value pairs from an upstream "tracestate" W3C header that we will propagate downstream.
    /// This value will _not_ include the "dd" key, which is parsed out into other individual values
    /// (e.g. sampling priority, origin, propagates tags, etc).
    /// </summary>
    string AdditionalW3CTraceState { get; set; }

    PathwayContext? PathwayContext { get; }

    [return: MaybeNull]
    TraceTagCollection PrepareTagsForPropagation();

    [return: MaybeNull]
    string PrepareTagsHeaderForPropagation();

    /// <summary>
    /// Sets a DataStreams checkpoint
    /// </summary>
    /// <param name="manager">The <see cref="DataStreamsManager"/> to use</param>
    /// <param name="checkpointKind">The type of the checkpoint</param>
    /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
    void SetCheckpoint(DataStreamsManager manager, CheckpointKind checkpointKind, string[] edgeTags);

    void MergePathwayContext(PathwayContext? pathwayContext);
}
