// <copyright file="SpanLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// A SpanLink is a lightweight representation of a Span.
/// A Span may have multiple SpanLinks and a SpanLink may represent a span from the same trace or from a different trace.
/// </summary>
internal class SpanLink
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanLink>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanLink"/> class.
    /// A span link describes a tuple of trace id and span id
    /// in OpenTelemetry that's called a Span Context, which may also include tracestate and trace flags.
    /// </summary>
    /// <param name="spanLinkContext">The context of the spanlink to extract attributes from</param>
    /// <param name="decoratedSpan">Reference to the span you're adding SpanLinks to</param>
    /// <param name="attributes">Optional dictionary of attributes to take for the spanlink.</param>
    public SpanLink(SpanContext spanLinkContext, Span decoratedSpan, List<KeyValuePair<string, string>>? attributes = null)
    {
        Context = spanLinkContext;
        DecoratedSpan = decoratedSpan;
        Attributes = attributes;
    }

    public SpanLink(Span spanToLink, Span decoratedSpan, List<KeyValuePair<string, string>>? attributes = null)
        : this(spanToLink.Context, decoratedSpan, attributes)
    {
    }

    public List<KeyValuePair<string, string>>? Attributes { get; private set; }

    public SpanContext Context { get;  }

    public Span DecoratedSpan { get; }

    /// <summary>
    /// Adds an Attribute to the SpanLink.
    /// </summary>
    /// <param name="name">name of attribute</param>
    /// <param name="value">value of attribute</param>
    public void AddAttribute(string name, string value)
    {
        if (DecoratedSpan.IsFinished)
        {
            Log.Warning("AddAttribute should not be called after the decorated span was closed");
            return;
        }

        var newAttribute = new KeyValuePair<string, string>(name, value);
        Attributes ??= [];

        Attributes.Add(newAttribute);
    }
}
