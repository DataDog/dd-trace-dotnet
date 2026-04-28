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
                    ref _globalInstanceLock);
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

                var createdSampler = _samplerFactory(DefaultSamplesPerSecond);
                if (_samplers.TryAdd(probeId, createdSampler))
                {
                    return createdSampler;
                }

                AdaptiveSamplerLifetime.Dispose(createdSampler);
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
            // We currently don't support updating the probe rate limit, and that is fine
            // since the functionality in the UI is not exposed yet.
            if (_samplers.TryGetValue(probeId, out _))
            {
                Log.Information("Adaptive sampler already exist for {ProbeID}", probeId);
                return;
            }

            var adaptiveSampler = _samplerFactory(samplesPerSecond);
            if (!_samplers.TryAdd(probeId, adaptiveSampler))
            {
                AdaptiveSamplerLifetime.Dispose(adaptiveSampler);
                Log.Information("Adaptive sampler already exist for {ProbeID}", probeId);
            }
        }

        public void ResetRate(string probeId)
        {
            if (_samplers.TryRemove(probeId, out var sampler))
            {
                AdaptiveSamplerLifetime.Dispose(sampler);
            }
        }
    }
}
