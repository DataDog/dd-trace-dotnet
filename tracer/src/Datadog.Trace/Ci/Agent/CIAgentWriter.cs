// <copyright file="CIAgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci.Agent
{
    /// <summary>
    /// CI Visibility Agent Writer
    /// </summary>
    internal class CIAgentWriter : IEventWriter
    {
        [ThreadStatic]
        private static Span[] _spanArray;
        private readonly AgentWriter _agentWriter;

        public CIAgentWriter(ImmutableTracerSettings settings, ISampler sampler, int maxBufferSize = 1024 * 1024 * 10)
        {
            var isPartialFlushEnabled = settings.Exporter.PartialFlushEnabled;
            var apiRequestFactory = TracesTransportStrategy.Get(settings.Exporter);
            var api = new Api(settings.Exporter.AgentUri, apiRequestFactory, null, rates => sampler.SetDefaultSampleRates(rates), isPartialFlushEnabled);
            _agentWriter = new AgentWriter(api, null, maxBufferSize: maxBufferSize);
        }

        public CIAgentWriter(IApi api, int maxBufferSize = 1024 * 1024 * 10)
        {
            _agentWriter = new AgentWriter(api, null, maxBufferSize: maxBufferSize);
        }

        public void WriteEvent(IEvent @event)
        {
            // To keep compatibility with the agent version of the payload, any IEvent conversion to span
            // goes here.

            if (_spanArray is not { } spanArray)
            {
                spanArray = new Span[1];
                _spanArray = spanArray;
            }

            if (@event is TestEvent testEvent)
            {
                spanArray[0] = testEvent.Content;
                WriteTrace(new ArraySegment<Span>(spanArray));
            }
            else if (@event is SpanEvent spanEvent)
            {
                spanArray[0] = spanEvent.Content;
                WriteTrace(new ArraySegment<Span>(spanArray));
            }
        }

        public Task FlushAndCloseAsync()
        {
            return _agentWriter.FlushAndCloseAsync();
        }

        public Task FlushTracesAsync()
        {
            return _agentWriter.FlushTracesAsync();
        }

        public Task<bool> Ping()
        {
            return _agentWriter.Ping();
        }

        public void WriteTrace(ArraySegment<Span> trace)
        {
            _agentWriter.WriteTrace(trace);
        }
    }
}
