// <copyright file="CIAgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Ci.Agent
{
    internal class CIAgentWriter : IAgentWriter
    {
        private readonly AgentWriter _agentWriter = null;

        public CIAgentWriter(TracerSettings settings)
        {
            _agentWriter = new AgentWriter(new Api(settings.AgentUri, TransportStrategy.Get(settings), null), null, maxBufferSize: settings.TraceBufferSize);
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
            // We ensure all spans in a trace has the origin tag (required for billing)
            foreach (var span in trace)
            {
                span.Context.Origin = TestTags.CIAppTestOriginName;
            }

            _agentWriter.WriteTrace(trace);
        }
    }
}
