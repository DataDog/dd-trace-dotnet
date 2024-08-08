// <copyright file="TestSuiteEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Internal;

namespace Datadog.Trace.Ci.EventModel;

internal class TestSuiteEvent : CIVisibilityEvent<Span>
{
    public TestSuiteEvent(Span span)
        : base(InternalSpanTypes.TestSuite, 1, span)
    {
    }
}
