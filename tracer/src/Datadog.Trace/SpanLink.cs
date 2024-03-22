// <copyright file="SpanLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

/// <summary>
/// The SpanLink contains the information needed for a decroated span for its Span Links.
/// </summary>
internal class SpanLink
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpanLink"/> class.
    /// A span link describes a tuple of trace id and span id
    /// in OpenTelemetry that's called a Span Context, which may also include tracestate and trace flags.
    /// </summary>
    /// <param name="traceId">The TraceId of the Span to be linked</param>
    /// <param name="spanId">The SpanId of the Span to be linked</param>
    public SpanLink(TraceId traceId, ulong spanId)
    {
        SpanId = spanId;
        TraceId = traceId;
    }

    internal TraceId TraceId { get; }

    internal ulong SpanId { get;  }
    // TODO: implement traceflags, tracestate, attributes - using spancontext

    // TODO - generate constructor that takes a W3C header and extracts the attribtues
}
