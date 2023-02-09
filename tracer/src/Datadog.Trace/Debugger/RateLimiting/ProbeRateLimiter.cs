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
    internal class ProbeRateLimiter
    {
        private const int DefaultSamplesPerSecond = 1;
        private const int DefaultGlobalSamplesPerSecond = DefaultSamplesPerSecond * 100;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeRateLimiter));

        private static object _globalInstanceLock = new();

        private static bool _globalInstanceInitialized;

        private static ProbeRateLimiter _instance;

        private readonly AdaptiveSampler _globalSampler = CreateSampler(DefaultGlobalSamplesPerSecond);

        private readonly ConcurrentDictionary<string, AdaptiveSampler> _samplers = new();

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

        public AdaptiveSampler GerOrAddSampler(string probeId)
        {
            return _samplers.GetOrAdd(probeId, _ => CreateSampler(1));
        }

        public bool Sample(string probeId)
        {
            // Rate limiter is engaged at ~1 probe per second (1 probes per 1s time window)
            var probeSampler = _samplers.GetOrAdd(probeId, _ => CreateSampler(1));
            return probeSampler.Sample() && _globalSampler.Sample();
        }

        public void SetRate(string probeId, int samplesPerSecond)
        {
            var adaptiveSampler = CreateSampler(samplesPerSecond);
            if (!_samplers.TryAdd(probeId, adaptiveSampler))
            {
                Log.Warning($"Failed to add sampler for probe {probeId}");
            }
        }

        public void ResetRate(string probeId)
        {
            _samplers.TryRemove(probeId, out _);
        }
    }
}
