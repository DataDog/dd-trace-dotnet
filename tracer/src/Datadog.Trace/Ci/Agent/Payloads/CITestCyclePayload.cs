// <copyright file="CITestCyclePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.EventModel;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal class CITestCyclePayload : EventsPayload
    {
        public override Uri Url { get; } = new Uri("https://citestcycle-intake.datad0g.com/api/v2/citestcycle");

        public override bool CanProcessEvent(IEvent @event)
        {
            // This intake accepts both Span and Test events
            if (@event is SpanEvent or TestEvent)
            {
                return true;
            }

            return false;
        }
    }
}
