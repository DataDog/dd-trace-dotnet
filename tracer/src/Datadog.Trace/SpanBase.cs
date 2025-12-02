// <copyright file="SpanBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace;

internal abstract class SpanBase
{
    internal abstract SpanContext Context { get; }

    internal bool IsRootSpan => Context.TraceContext?.RootSpan == this;

    /// <summary>
    /// Gets the resource name
    /// </summary>
    internal abstract string? ResourceName { get; }

    /// <summary>
    /// Gets the service name
    /// </summary>
    internal string? ServiceName => Context.ServiceName;

    /// <summary>
    /// Gets <i>local root span id</i>, i.e. the <c>SpanId</c> of the span that is the root of the local, non-reentrant
    /// sub-operation of the distributed operation that is represented by the trace that contains this span.
    /// </summary>
    /// <remarks>
    /// <para>If the trace has been propagated from a remote service, the <i>remote global root</i> is not relevant for this API.</para>
    /// <para>A distributed operation represented by a trace may be re-entrant (e.g. service-A calls service-B, which calls service-A again).
    /// In such cases, the local process may be concurrently executing multiple local root spans.
    /// This API returns the id of the root span of the non-reentrant trace sub-set.</para></remarks>
    internal ulong RootSpanId => Context.TraceContext?.RootSpan?.SpanId ?? SpanId;

    /// <summary>
    /// Gets the trace's unique 128-bit identifier.
    /// </summary>
    internal TraceId TraceId128 => Context.TraceId128;

    /// <summary>
    /// Gets the 64-bit trace id, or the lower 64 bits of a 128-bit trace id.
    /// </summary>
    internal ulong TraceId => Context.TraceId128.Lower;

    /// <summary>
    /// Gets the span's unique 64-bit identifier.
    /// </summary>
    internal ulong SpanId => Context.SpanId;

    /// <summary>
    /// Gets or sets the type of request this span represents (ex: web, db).
    /// Not to be confused with span kind.
    /// </summary>
    /// <seealso cref="SpanTypes"/>
    internal string? Type { get; set; }

    /// <summary>
    /// Record the end time of the span and flushes it to the backend.
    /// After the span has been finished all modifications will be ignored.
    /// </summary>
    internal abstract void Finish();

    /// <summary>
    /// Explicitly set the end time of the span and flushes it to the backend.
    /// After the span has been finished all modifications will be ignored.
    /// </summary>
    /// <param name="finishTimestamp">Explicit value for the end time of the Span</param>
    internal abstract void Finish(DateTimeOffset finishTimestamp);

    internal abstract void Finish(TimeSpan duration);
}
