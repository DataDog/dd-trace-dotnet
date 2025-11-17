// <copyright file="ProbeMetrics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Concurrent;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal enum RejectionReason
    {
        KillSwitch,
        ThreadLocalPrefilter,
        GlobalBudgetExhausted,
        CircuitOpen,
        AdaptiveSampler
    }

    /// <summary>
    /// Collects metrics for debugger rate limiting and circuit breaker
    /// </summary>
    internal class ProbeMetrics
    {
        private readonly ConcurrentDictionary<string, ProbeStats> _stats = new();

        public void RecordSample(string probeId, bool accepted, CaptureBehaviour behaviour, long elapsedTicks)
        {
            var stats = _stats.GetOrAdd(probeId, _ => new ProbeStats());
            stats.RecordSample(accepted, behaviour, elapsedTicks);
        }

        public void RecordRejection(string probeId, RejectionReason reason)
        {
            var stats = _stats.GetOrAdd(probeId, _ => new ProbeStats());
            stats.RecordRejection(reason);
        }

        public ProbeStats? GetStats(string probeId)
        {
            return _stats.TryGetValue(probeId, out var stats) ? stats : null;
        }

        public void Reset()
        {
            _stats.Clear();
        }
    }
}
