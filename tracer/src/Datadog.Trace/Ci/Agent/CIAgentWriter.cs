// <copyright file="CIAgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Ci.Agent
{
    internal class CIAgentWriter : IAgentWriter
    {
        private readonly AgentWriter _agentWriter = null;
        private readonly bool _isPartialFlushEnabled = false;

        public CIAgentWriter(TracerSettings settings)
        {
            _isPartialFlushEnabled = settings.PartialFlushEnabled;
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
            // We ensure there's no trace (local root span) without a test tag.
            // And ensure all other spans have the origin tag.

            // Check if the trace has any span
            if (trace.Count == 0)
            {
                // No trace to write
                return;
            }

            if (!_isPartialFlushEnabled)
            {
                // Check if the last span (the root) is a test, bechmark or build span
                Span lastSpan = trace.Array[trace.Offset + trace.Count - 1];
                if (lastSpan.Context.Parent is null &&
                    lastSpan.Type != SpanTypes.Test &&
                    lastSpan.Type != SpanTypes.Benchmark &&
                    lastSpan.Type != SpanTypes.Build)
                {
                    CIVisibility.Log.Warning<int>("Spans dropped because not having a test or benchmark root span: {Count}", trace.Count);
                    return;
                }
            }

            foreach (var span in trace)
            {
                // Sets the origin tag to any other spans to ensure the CI track.
                span.Context.Origin = TestTags.CIAppTestOriginName;
            }

            _agentWriter.WriteTrace(trace);
        }
    }
}
