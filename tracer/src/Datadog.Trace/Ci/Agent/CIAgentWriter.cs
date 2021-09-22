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
            // We ensure there's no trace (local root span) without a test tag.
            // And ensure all remaining spans have the origin tag.

            HashSet<ulong> removeIds = null;
            Span[] finalTrace = new Span[trace.Count];
            int idx = 0;
            foreach (var span in trace)
            {
                // Remove traces non test, benchmarks or build traces.
                if (span.Context.Parent is null)
                {
                    if (span.Type != SpanTypes.Test &&
                        span.Type != SpanTypes.Benchmark &&
                        span.Type != SpanTypes.Build)
                    {
                        if (removeIds == null)
                        {
                            removeIds = new HashSet<ulong>();
                        }

                        removeIds.Add(span.SpanId);
                        CIVisibility.Log.Warning($"Non Test or Benchmark trace was dropped: {span}");
                        continue;
                    }
                }
                else if (removeIds != null && removeIds.Contains(span.Context.ParentId.Value))
                {
                    removeIds.Add(span.SpanId);
                    CIVisibility.Log.Warning($"Non Test or Benchmark trace was dropped: {span}");
                    continue;
                }

                // Sets the origin tag to any other spans to ensure the CI track.
                span.Context.Origin = TestTags.CIAppTestOriginName;
                finalTrace[idx++] = span;
            }

            _agentWriter.WriteTrace(new ArraySegment<Span>(finalTrace, 0, idx));
        }
    }
}
