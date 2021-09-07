// <copyright file="MockTracerAgentAppSecWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.EventModel.Batch;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Security.IntegrationTests
{
    internal class MockTracerAgentAppSecWrapper
    {
        private readonly MockTracerAgent agent;
        private readonly List<IEvent> events;

        public MockTracerAgentAppSecWrapper(MockTracerAgent agent)
        {
            this.agent = agent;
            events = new List<IEvent>();
        }

        internal IImmutableList<IEvent> WaitForAppSecEvents(
            int count,
            int timeoutInMilliseconds = 20000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
            var events = ImmutableList<IEvent>.Empty;
            while (DateTime.Now < deadline)
            {
                events = this.events.ToImmutableList();
                if (events.Count >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            return events;
        }

        internal void SubscribeAppSecEvents()
        {
            agent.RequestReceived += Agent_RequestReceived;
        }

        internal void UnsubscribeAppSecEvents()
        {
            agent.RequestReceived -= Agent_RequestReceived;
        }

        internal void Agent_RequestReceived(object sender, EventArgs<System.Net.HttpListenerContext> ctx)
        {
            var appSecUrl = ctx.Value.Request.Url.AbsoluteUri.Contains("appsec");
            if (appSecUrl)
            {
                using var sr = new StreamReader(ctx.Value.Request.InputStream);
                string content = sr.ReadToEnd();
                var intake = JsonConvert.DeserializeObject<Intake>(content, new IntakeConverter());
                events.AddRange(intake.Events);
            }
        }
    }
}
