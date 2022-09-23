// <copyright file="CITestCyclePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal class CITestCyclePayload : CIVisibilityProtocolPayload
    {
        public CITestCyclePayload(CIVisibilitySettings settings, IFormatterResolver formatterResolver = null)
            : base(settings, formatterResolver)
        {
        }

        public override string EventPlatformSubdomain => "citestcycle-intake";

        public override string EventPlatformPath => "api/v2/citestcycle";

        public override bool CanProcessEvent(IEvent @event)
        {
            // This intake accepts both Span and Test events
            if (@event is SpanEvent
                or TestEvent
                or TestSuiteEvent
                or TestModuleEvent
                or TestSessionEvent)
            {
                return true;
            }

            return false;
        }
    }
}
