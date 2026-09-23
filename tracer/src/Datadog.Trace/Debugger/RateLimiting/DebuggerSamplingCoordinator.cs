// <copyright file="DebuggerSamplingCoordinator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal sealed class DebuggerSamplingCoordinator
    {
        private HashSet<string>? _emittedProbeIds;
        private int _decision;

        private enum Decision
        {
            Undecided,
            Creating,
            DropGlobal,
            DropProbe,
            Keep
        }

        internal DebuggerSamplingDecision TrySample<TSamplingDecisionProvider>(string probeId, TSamplingDecisionProvider samplingDecisionProvider)
            where TSamplingDecisionProvider : struct, IDebuggerSamplingDecisionProvider
        {
            var currentDecision = (Decision)Volatile.Read(ref _decision);
            if (currentDecision is Decision.DropGlobal or Decision.DropProbe)
            {
                return ToSamplingDecision(currentDecision);
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
                        return DebuggerSamplingDecision.DropProbe;
                    case Decision.DropGlobal:
                        return DebuggerSamplingDecision.DropGlobal;
                    case Decision.DropProbe:
                        return DebuggerSamplingDecision.DropProbe;
                    case Decision.Keep:
                        return _emittedProbeIds!.Add(probeId)
                                   ? DebuggerSamplingDecision.Keep
                                   : DebuggerSamplingDecision.DropProbe;
                }

                _decision = (int)Decision.Creating;
                try
                {
                    var samplingDecision = samplingDecisionProvider.Sample();
                    switch (samplingDecision)
                    {
                        case DebuggerSamplingDecision.DropGlobal:
                            Volatile.Write(ref _decision, (int)Decision.DropGlobal);
                            return samplingDecision;
                        case DebuggerSamplingDecision.DropProbe:
                            Volatile.Write(ref _decision, (int)Decision.DropProbe);
                            return samplingDecision;
                        case DebuggerSamplingDecision.Keep:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(samplingDecision), samplingDecision, null);
                    }

                    _emittedProbeIds = new HashSet<string> { probeId };
                    Volatile.Write(ref _decision, (int)Decision.Keep);
                    return DebuggerSamplingDecision.Keep;
                }
                catch
                {
                    _decision = (int)Decision.Undecided;
                    throw;
                }
            }
        }

        private static DebuggerSamplingDecision ToSamplingDecision(Decision decision)
            => decision == Decision.DropGlobal
                   ? DebuggerSamplingDecision.DropGlobal
                   : DebuggerSamplingDecision.DropProbe;
    }
}
