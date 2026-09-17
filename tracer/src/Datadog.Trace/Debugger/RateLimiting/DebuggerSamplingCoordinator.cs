// <copyright file="DebuggerSamplingCoordinator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal static class DebuggerSamplingCoordinator
    {
        internal static bool TrySample<TSamplingDecisionProvider>(ref State? state, Span? rootSpan, string probeId, TSamplingDecisionProvider samplingDecisionProvider)
            where TSamplingDecisionProvider : struct, IDebuggerSamplingDecisionProvider
        {
            var current = Volatile.Read(ref state);
            if (current is null)
            {
                if (rootSpan is null)
                {
                    return samplingDecisionProvider.Sample();
                }

                lock (rootSpan)
                {
                    current = Volatile.Read(ref state);
                    if (current is null)
                    {
                        var created = State.Create(samplingDecisionProvider.Sample());
                        current = Interlocked.CompareExchange(ref state, created, null) ?? created;
                    }
                }
            }

            return current.TryEmit(probeId);
        }

        internal sealed class State
        {
            private static readonly State Drop = new(shouldEmit: false);

            private readonly HashSet<string>? _emittedProbeIds;

            private State(bool shouldEmit)
            {
                _emittedProbeIds = shouldEmit ? new HashSet<string>() : null;
            }

            public static State Create(bool shouldEmit)
                => shouldEmit ? new State(shouldEmit: true) : Drop;

            public bool TryEmit(string probeId)
            {
                var emittedProbeIds = _emittedProbeIds;
                if (emittedProbeIds is null)
                {
                    return false;
                }

                lock (emittedProbeIds)
                {
                    return emittedProbeIds.Add(probeId);
                }
            }
        }
    }
}
