// <copyright file="CIVisibilityEventsFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;

namespace Datadog.Trace.Ci.EventModel;

internal static class CIVisibilityEventsFactory
{
    public static IEvent FromSpan(Span span)
    {
        return span.Type switch
        {
            SpanTypes.Test or SpanTypes.Browser => span.Tags is TestSpanTags ? new TestEvent(span) : new TestEvent(span, 1),
            SpanTypes.TestSuite when span.Tags is TestSuiteSpanTags => new TestSuiteEvent(span),
            SpanTypes.TestModule when span.Tags is TestModuleSpanTags => new TestModuleEvent(span),
            SpanTypes.TestSession => new TestSessionEvent(span),
            _ => new SpanEvent(span)
        };
    }

    public static Span? GetSpan(IEvent @event)
    {
        if (@event is CIVisibilityEvent<Span> ciVisibilityEvent)
        {
            return ciVisibilityEvent.Content;
        }

        return null;
    }
}
