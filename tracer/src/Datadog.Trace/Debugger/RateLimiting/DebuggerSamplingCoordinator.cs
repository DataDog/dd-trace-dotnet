// <copyright file="DebuggerSamplingCoordinator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal sealed class DebuggerSamplingCoordinator
    {
        private HashSet<string>? _emittedProbeIds;
        private int _decision;

        internal DebuggerSamplingDecision TrySample<TSamplingDecisionProvider>(string probeId, TSamplingDecisionProvider samplingDecisionProvider)
            where TSamplingDecisionProvider : struct, IDebuggerSamplingDecisionProvider
        {
            var decision = (DebuggerSamplingDecision)Volatile.Read(ref _decision);
            if (decision is DebuggerSamplingDecision.DropGlobal or DebuggerSamplingDecision.DropProbe)
            {
                return decision;
            }

            // The lock is reentrant. Holding it here means Sample() ran customer code on this thread
            // (for example a first-chance exception handler) that hit another probe in this trace.
            if (Monitor.IsEntered(this))
            {
                return DebuggerSamplingDecision.DropProbe;
            }

            lock (this)
            {
                if (_decision == (int)DebuggerSamplingDecision.Undecided)
                {
                    var samplingDecision = samplingDecisionProvider.Sample();
                    if (samplingDecision == DebuggerSamplingDecision.Keep)
                    {
                        _emittedProbeIds = [probeId];
                    }

                    _decision = (int)samplingDecision;
                    return samplingDecision;
                }

                if (_decision == (int)DebuggerSamplingDecision.Keep)
                {
                    return _emittedProbeIds!.Add(probeId)
                               ? DebuggerSamplingDecision.Keep
                               : DebuggerSamplingDecision.DropProbe;
                }

                return (DebuggerSamplingDecision)_decision;
            }
        }
    }
}
