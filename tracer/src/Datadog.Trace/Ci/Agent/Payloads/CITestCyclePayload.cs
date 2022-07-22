// <copyright file="CITestCyclePayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    internal class CITestCyclePayload : CIVisibilityProtocolPayload
    {
        public CITestCyclePayload(IFormatterResolver formatterResolver = null)
            : base(formatterResolver)
        {
            var agentlessUrl = CIVisibility.Settings.AgentlessUrl;
            if (!string.IsNullOrWhiteSpace(agentlessUrl))
            {
                var builder = new UriBuilder(agentlessUrl);
                builder.Path = "api/v2/citestcycle";
                Url = builder.Uri;
            }
            else
            {
                Url = new UriBuilder(
                    scheme: "https",
                    host: "citestcycle-intake." + CIVisibility.Settings.Site,
                    port: 443,
                    pathValue: "api/v2/citestcycle").Uri;
            }
        }

        public override Uri Url { get; }

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
