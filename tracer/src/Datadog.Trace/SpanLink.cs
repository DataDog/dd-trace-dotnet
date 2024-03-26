// <copyright file="SpanLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Propagators;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// The SpanLink contains the information needed for a decorated span for its Span Links.
/// </summary>
internal class SpanLink
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpanLink"/> class.
    /// A span link describes a tuple of trace id and span id
    /// in OpenTelemetry that's called a Span Context, which may also include tracestate and trace flags.
    /// </summary>
    /// <param name="spanLinkContext">The context of the spanlink to extract attributes from</param>
    /// <param name="optionalAttributes">Optional dictionary of attributes to take for the spanlink.</param>
    internal SpanLink(SpanContext spanLinkContext, List<KeyValuePair<string, object>>? optionalAttributes)
    {
        Context = spanLinkContext;
        Attributes = optionalAttributes;
    }

    internal SpanLink(Span spanToLink, List<KeyValuePair<string, object>>? optionalAttributes)
        : this(spanToLink.Context, optionalAttributes)
    {
    }

    internal List<KeyValuePair<string, object>>? Attributes { get;  }

    internal SpanContext Context { get;  }
}
