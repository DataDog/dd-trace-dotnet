// <copyright file="ProbeRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal sealed class ProbeRateLimiter
    {
        private const int DefaultSamplesPerSecond = 1;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeRateLimiter));

        private static object _globalInstanceLock = new();

        private static bool _globalInstanceInitialized;

        private static ProbeRateLimiter _instance;

        private readonly ConcurrentDictionary<string, IAdaptiveSampler> _samplers = new();

        internal static ProbeRateLimiter Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock);
            }
        }

        private static AdaptiveSampler CreateSampler(int samplesPerSecond = DefaultSamplesPerSecond) =>
            new(TimeSpan.FromSeconds(1), samplesPerSecond, 180, 16, null);

        public IAdaptiveSampler GerOrAddSampler(string probeId)
        {
            // Hot path: a sampler is almost always already present (added on probe instrumentation
            // via SetRate/TryAddSampler). The TryGetValue fast path keeps that case allocation-free.
            //
            // We deliberately avoid ConcurrentDictionary.GetOrAdd(key, factory) because
            // CreateSampler() allocates a System.Threading.Timer, and the runtime roots that Timer
            // in its global timer queue. If GetOrAdd's factory is invoked but its TryAdd loses the
            // race (a documented possibility), the losing AdaptiveSampler and its Timer would be
            // leaked permanently.
            if (_samplers.TryGetValue(probeId, out var sampler))
            {
                return sampler;
            }

            var candidate = CreateSampler();
            if (_samplers.TryAdd(probeId, candidate))
            {
                return candidate;
            }

            candidate.Dispose();
            return _samplers.TryGetValue(probeId, out sampler) ? sampler : candidate;
        }

        public bool TryAddSampler(string probeId, IAdaptiveSampler sampler)
        {
            return _samplers.TryAdd(probeId, sampler);
        }

        public void SetRate(string probeId, int samplesPerSecond)
        {
            if (_samplers.TryGetValue(probeId, out var existing))
            {
                UpdateExistingRate(probeId, existing, samplesPerSecond);
                return;
            }

            var candidate = CreateSampler(samplesPerSecond);
            if (_samplers.TryAdd(probeId, candidate))
            {
                return;
            }

            // Lost the insert race - apply the new rate to whoever won and dispose our candidate
            // so its Timer doesn't get leaked into the runtime's timer queue.
            if (_samplers.TryGetValue(probeId, out existing))
            {
                UpdateExistingRate(probeId, existing, samplesPerSecond);
            }

            candidate.Dispose();
        }

        public void ResetRate(string probeId)
        {
            // Disposing the removed sampler is critical: AdaptiveSampler owns a Timer that the
            // runtime keeps alive in its timer queue until disposed, which would otherwise pin the
            // sampler (and its captured callback graph) for the lifetime of the process every time
            // a probe is removed.
            if (_samplers.TryRemove(probeId, out var removed))
            {
                removed.Dispose();
            }
        }

        private static void UpdateExistingRate(string probeId, IAdaptiveSampler existing, int samplesPerSecond)
        {
            if (existing is AdaptiveSampler adaptive)
            {
                adaptive.SetRate(samplesPerSecond);
            }
            else
            {
                // Non-AdaptiveSampler entries (e.g. NopAdaptiveSampler for span decoration / metric
                // probes) intentionally don't honor a rate; nothing to update.
                Log.Information("Adaptive sampler for {ProbeID} cannot be updated", probeId);
            }
        }
    }
}
