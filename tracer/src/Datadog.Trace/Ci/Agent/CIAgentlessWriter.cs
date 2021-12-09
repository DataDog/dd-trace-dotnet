// <copyright file="CIAgentlessWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci.Agent
{
    internal class CIAgentlessWriter : IAgentWriter
    {
        private readonly AgentWriter _agentWriter = null;

        public CIAgentlessWriter(ImmutableTracerSettings settings, ISampler sampler)
        {
            var api = new Api(settings.AgentUri, TransportStrategy.Get(settings), null, rates => sampler.SetDefaultSampleRates(rates), settings.PartialFlushEnabled);
            _agentWriter = new AgentWriter(api, null, maxBufferSize: settings.TraceBufferSize);
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
