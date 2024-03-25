// <copyright file="SpanLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Propagators;

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
    internal SpanLink(TraceId traceId, ulong spanId)
    {
        SpanId = spanId;
        TraceId = traceId;
    }

    internal SpanLink(SpanContext spanLinkContext, Dictionary<string, object> optionalAttributes)
    {
        TraceId = spanLinkContext.TraceId128;
        SpanId = spanLinkContext.SpanId;
        // this is to avoid making the dictionary if the user isn't adding attributes - better perf if I recall correctly
        if (optionalAttributes is not null)
        {
            Attributes = optionalAttributes;
        }

        // Where do I even find the the properties below?
        TraceState = W3CTraceContextPropagator.CreateTraceStateHeader(spanLinkContext);
        // 3 possible values, 1, 0 or null
        var samplingPriority = spanLinkContext.TraceContext?.SamplingPriority ?? spanLinkContext.SamplingPriority;
        TraceFlags = samplingPriority switch
        {
            null => 0,
            > 0 => 1u + (1u << 31),
            <= 0 => 1u << 31,
        };

        // TraceFlags = spanLinkContext.traceFlags;
    }

    internal SpanLink(Span spanToLink, Dictionary<string, object> optionalAttributes)
        : this(spanToLink.Context, optionalAttributes)
    {
    }

    internal TraceId TraceId { get; }

    internal ulong SpanId { get;  }

    internal string TraceState { get;  }

    internal uint TraceFlags { get; }

    internal Dictionary<string, object> Attributes { get;  }

    internal SpanContext SpanLinkContext { get;  }

    // TODO: implement traceflags, tracestate, attributes - using spancontext

    // TODO - generate constructor that takes a W3C header and extracts the attributes
}
