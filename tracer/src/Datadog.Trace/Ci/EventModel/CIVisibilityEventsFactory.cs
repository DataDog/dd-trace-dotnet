// <copyright file="CIVisibilityEventsFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.EventModel;

internal static class CIVisibilityEventsFactory
{
    public static IEvent FromSpan(Span span)
    {
        switch (span.Type)
        {
            case SpanTypes.Test:
                return new TestEvent(span);
            case SpanTypes.TestSuite:
                return new TestSuiteEvent(span);
            case SpanTypes.TestModule:
                return new TestModuleEvent(span);
            case SpanTypes.TestSession:
                return new TestSessionEvent(span);
        }

        return new SpanEvent(span);
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
