// <copyright file="CIVisibilityEventsFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.EventModel;

internal static class CIVisibilityEventsFactory
{
    public static IEvent FromSpan(Span span)
        => span.Type switch
        {
            SpanTypes.Test => new TestEvent(span),
            SpanTypes.TestSuite => new TestSuiteEvent(span),
            SpanTypes.TestModule => new TestModuleEvent(span),
            SpanTypes.TestSession => new TestSessionEvent(span),
            _ => new SpanEvent(span)
        };

    public static Span? GetSpan(IEvent @event)
    {
        if (@event is CIVisibilityEvent<Span> ciVisibilityEvent)
        {
            return ciVisibilityEvent.Content;
        }

        return null;
    }
}
