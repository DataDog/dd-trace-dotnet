// <copyright file="CIVisibilityEventsFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Internal;

namespace Datadog.Trace.Ci.EventModel;

internal static class CIVisibilityEventsFactory
{
    public static IEvent FromSpan(Span span)
    {
        return span.Type switch
        {
            InternalSpanTypes.Test or InternalSpanTypes.Browser => span.Tags is TestSpanTags ? new TestEvent(span) : new TestEvent(span, 1),
            InternalSpanTypes.TestSuite when span.Tags is TestSuiteSpanTags => new TestSuiteEvent(span),
            InternalSpanTypes.TestModule when span.Tags is TestModuleSpanTags => new TestModuleEvent(span),
            InternalSpanTypes.TestSession => new TestSessionEvent(span),
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
