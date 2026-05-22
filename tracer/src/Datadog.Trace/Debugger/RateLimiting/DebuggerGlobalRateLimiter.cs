// <copyright file="DebuggerGlobalRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal sealed class DebuggerGlobalRateLimiter : IDebuggerGlobalRateLimiter
    {
        internal const int DefaultSnapshotSamplesPerSecond = 100;

        private const int LogCooldownSeconds = 60;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerGlobalRateLimiter));

        private readonly Func<int, IAdaptiveSampler> _samplerFactory;
        private readonly ILogRateLimiter _logRateLimiter;
        private readonly object _lifetimeLock = new();

        private IAdaptiveSampler _snapshotSampler;
        private bool _disposed;

        internal DebuggerGlobalRateLimiter()
            : this(AdaptiveSamplerLifetime.Create, new LogRateLimiter(LogCooldownSeconds))
        {
        }

        internal DebuggerGlobalRateLimiter(Func<int, IAdaptiveSampler> samplerFactory, ILogRateLimiter logRateLimiter)
        {
            _samplerFactory = samplerFactory ?? throw new ArgumentNullException(nameof(samplerFactory));
            _logRateLimiter = logRateLimiter ?? throw new ArgumentNullException(nameof(logRateLimiter));
            _snapshotSampler = NopAdaptiveSampler.Instance;
            ResetRate();
        }

        internal static DebuggerGlobalRateLimiter Instance { get; } = new();

        public bool ShouldSampleSnapshot(string probeId)
        {
            var sampler = Volatile.Read(ref _snapshotSampler);

            if (sampler.Sample())
            {
                return true;
            }

            LogDrop(probeId);
            return false;
        }

        public void Initialize()
        {
            lock (_lifetimeLock)
            {
                _disposed = false;
                ReplaceSampler(DefaultSnapshotSamplesPerSecond);
            }
        }

        public void SetRate(double? samplesPerSecond)
        {
            lock (_lifetimeLock)
            {
                if (_disposed)
                {
                    return;
                }

                if (!samplesPerSecond.HasValue)
                {
                    ReplaceSampler(DefaultSnapshotSamplesPerSecond);
                    return;
                }

                var configuredRate = Math.Max((int)samplesPerSecond.Value, 0);
                ReplaceSampler(configuredRate);
            }
        }

        public void ResetRate()
        {
            lock (_lifetimeLock)
            {
                if (_disposed)
                {
                    return;
                }

                ReplaceSampler(DefaultSnapshotSamplesPerSecond);
            }
        }

        public void Dispose()
        {
            lock (_lifetimeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                AdaptiveSamplerLifetime.Dispose(ref _snapshotSampler);
            }
        }

        private void ReplaceSampler(int snapshotSamplesPerSecond)
        {
            AdaptiveSamplerLifetime.Replace(ref _snapshotSampler, _samplerFactory(snapshotSamplesPerSecond));
        }

        private void LogDrop(string probeId, [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            if (!_logRateLimiter.ShouldLog(sourceFile, sourceLine, out var skipCount))
            {
                return;
            }

            const string message = "Global debugger rate limit reached for snapshot probes. Dropping capture for ProbeId={ProbeId}";
            const string messageWithSkipCount = "Global debugger rate limit reached for snapshot probes. Dropping capture for ProbeId={ProbeId}, {SkipCount} additional messages skipped";

            if (skipCount > 0)
            {
                Log.Warning(messageWithSkipCount, probeId, skipCount);
            }
            else
            {
                Log.Warning(message, probeId);
            }
        }
    }
}
