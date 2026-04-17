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

        private readonly Func<int, IAdaptiveSampler> _samplerFactory;
        private readonly ConcurrentDictionary<string, IAdaptiveSampler> _samplers = new();

        internal ProbeRateLimiter()
            : this(AdaptiveSamplerLifetime.Create)
        {
        }

        internal ProbeRateLimiter(Func<int, IAdaptiveSampler> samplerFactory)
        {
            _samplerFactory = samplerFactory ?? throw new ArgumentNullException(nameof(samplerFactory));
        }

        internal static ProbeRateLimiter Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock,
                    () => new ProbeRateLimiter());
            }
        }

        public IAdaptiveSampler GerOrAddSampler(string probeId)
        {
            while (true)
            {
                if (_samplers.TryGetValue(probeId, out var sampler))
                {
                    return sampler;
                }

                var candidate = _samplerFactory(DefaultSamplesPerSecond);
                if (_samplers.TryAdd(probeId, candidate))
                {
                    return candidate;
                }

                AdaptiveSamplerLifetime.Dispose(candidate);
            }
        }

        public bool TryAddSampler(string probeId, IAdaptiveSampler sampler)
        {
            if (_samplers.TryAdd(probeId, sampler))
            {
                return true;
            }

            AdaptiveSamplerLifetime.Dispose(sampler);
            return false;
        }

        public void SetRate(string probeId, int samplesPerSecond)
        {
            if (_samplers.TryGetValue(probeId, out var existing))
            {
                UpdateExistingRate(probeId, existing, samplesPerSecond);
                return;
            }

            var candidate = _samplerFactory(samplesPerSecond);
            if (_samplers.TryAdd(probeId, candidate))
            {
                return;
            }

            // Lost the insert race - apply the new rate to the winner and dispose our candidate.
            if (_samplers.TryGetValue(probeId, out existing))
            {
                UpdateExistingRate(probeId, existing, samplesPerSecond);
            }

            AdaptiveSamplerLifetime.Dispose(candidate);
        }

        public void ResetRate(string probeId)
        {
            // Must dispose: AdaptiveSampler's Timer is rooted by the runtime until disposed.
            if (_samplers.TryRemove(probeId, out var removed))
            {
                AdaptiveSamplerLifetime.Dispose(removed);
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
                // NopAdaptiveSampler entries (span decoration / metric probes) don't honor a rate.
                Log.Information("Adaptive sampler for {ProbeID} cannot be updated", probeId);
            }
        }
    }
}
