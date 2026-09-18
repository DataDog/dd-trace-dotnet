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
        internal static bool TrySample<TSamplingDecisionProvider>(ref State? state, string probeId, TSamplingDecisionProvider samplingDecisionProvider)
            where TSamplingDecisionProvider : struct, IDebuggerSamplingDecisionProvider
        {
            var current = Volatile.Read(ref state);
            if (current is null)
            {
                var created = new State();
                current = Interlocked.CompareExchange(ref state, created, null) ?? created;
            }

            return current.TrySample(probeId, samplingDecisionProvider);
        }

        internal sealed class State
        {
            private HashSet<string>? _emittedProbeIds;
            private int _decision;

            private enum Decision
            {
                Undecided,
                Creating,
                Drop,
                Keep
            }

            internal bool TrySample<TSamplingDecisionProvider>(string probeId, TSamplingDecisionProvider samplingDecisionProvider)
                where TSamplingDecisionProvider : struct, IDebuggerSamplingDecisionProvider
            {
                if ((Decision)Volatile.Read(ref _decision) == Decision.Drop)
                {
                    return false;
                }

                lock (this)
                {
                    switch ((Decision)_decision)
                    {
                        case Decision.Undecided:
                            break;
                        case Decision.Creating:
                            // Monitor locks are reentrant, so this is a nested sample on the deciding
                            // thread. Other threads cannot enter until the decision is published.
                            return false;
                        case Decision.Drop:
                            return false;
                        case Decision.Keep:
                            return _emittedProbeIds!.Add(probeId);
                    }

                    _decision = (int)Decision.Creating;
                    try
                    {
                        if (!samplingDecisionProvider.Sample())
                        {
                            Volatile.Write(ref _decision, (int)Decision.Drop);
                            return false;
                        }

                        _emittedProbeIds = new HashSet<string> { probeId };
                        Volatile.Write(ref _decision, (int)Decision.Keep);
                        return true;
                    }
                    catch
                    {
                        _decision = (int)Decision.Undecided;
                        throw;
                    }
                }
            }
        }
    }
}
