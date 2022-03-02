// <copyright file="TestEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.EventModel
{
    internal class TestEvent : CIVisibilityEvent<Span>
    {
        public TestEvent(Span span)
            : base("test", 1, span)
        {
        }
    }
}
