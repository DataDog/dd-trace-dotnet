// <copyright file="SpanLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Propagators;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// A SpanLink is a lightweight representation of a Span.
/// A Span may have multiple SpanLinks and a SpanLink may represent a span from the same trace or from a different trace.
/// </summary>
internal class SpanLink
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpanLink"/> class.
    /// A span link describes a tuple of trace id and span id
    /// in OpenTelemetry that's called a Span Context, which may also include tracestate and trace flags.
    /// </summary>
    /// <param name="spanLinkContext">The context of the spanlink to extract attributes from</param>
    /// <param name="attributes">Optional dictionary of attributes to take for the spanlink.</param>
    internal SpanLink(SpanContext spanLinkContext, List<KeyValuePair<string, string>>? attributes = null)
    {
        Context = spanLinkContext;
        Attributes = attributes;
    }

    internal SpanLink(Span spanToLink, List<KeyValuePair<string, string>>? attributes = null)
        : this(spanToLink.Context, attributes)
    {
    }

    internal List<KeyValuePair<string, string>>? Attributes { get; set; }

    internal SpanContext Context { get;  }

    internal SpanLink AddAttribute(string name, string value)
    {
        var newAttribute = new KeyValuePair<string, string>(name, value);
        if (Attributes is null)
        {
            Attributes = new List<KeyValuePair<string, string>>();
        }

        Attributes.Add(newAttribute);
        return this;
    }
}
