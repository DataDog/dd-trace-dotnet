// <copyright file="DebuggerGlobalRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal sealed class DebuggerGlobalRateLimiter : IDebuggerGlobalRateLimiter
    {
        internal const int DefaultSnapshotSamplesPerSecond = 100;
        internal const int DefaultLogSamplesPerSecond = 5000;

        private const int LogCooldownSeconds = 60;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerGlobalRateLimiter));

        private readonly Func<int, IAdaptiveSampler> _samplerFactory;
        private readonly ILogRateLimiter _logRateLimiter;

        private IAdaptiveSampler _snapshotSampler;
        private IAdaptiveSampler _logSampler;

        internal DebuggerGlobalRateLimiter()
            : this(AdaptiveSamplerLifetime.Create, new LogRateLimiter(LogCooldownSeconds))
        {
        }

        internal DebuggerGlobalRateLimiter(Func<int, IAdaptiveSampler> samplerFactory, ILogRateLimiter logRateLimiter)
        {
            _samplerFactory = samplerFactory ?? throw new ArgumentNullException(nameof(samplerFactory));
            _logRateLimiter = logRateLimiter ?? throw new ArgumentNullException(nameof(logRateLimiter));
            _snapshotSampler = NopAdaptiveSampler.Instance;
            _logSampler = NopAdaptiveSampler.Instance;
            ResetRate();
        }

        internal static DebuggerGlobalRateLimiter Instance { get; } = new();

        public bool ShouldSample(ProbeType probeType, string probeId)
        {
            var sampler = probeType switch
            {
                ProbeType.Snapshot => _snapshotSampler,
                ProbeType.Log => _logSampler,
                _ => null
            };

            if (sampler == null || sampler.Sample())
            {
                return true;
            }

            LogDrop(probeType, probeId);
            return false;
        }

        public void SetRate(double? samplesPerSecond)
        {
            if (!samplesPerSecond.HasValue)
            {
                ResetRate();
                return;
            }

            var configuredRate = Math.Max((int)samplesPerSecond.Value, 0);
            ReplaceSamplers(configuredRate, configuredRate);
        }

        public void ResetRate()
        {
            ReplaceSamplers(DefaultSnapshotSamplesPerSecond, DefaultLogSamplesPerSecond);
        }

        public void Dispose()
        {
            AdaptiveSamplerLifetime.Dispose(ref _snapshotSampler);
            AdaptiveSamplerLifetime.Dispose(ref _logSampler);
        }

        private void ReplaceSamplers(int snapshotSamplesPerSecond, int logSamplesPerSecond)
        {
            AdaptiveSamplerLifetime.Replace(ref _snapshotSampler, _samplerFactory(snapshotSamplesPerSecond));
            AdaptiveSamplerLifetime.Replace(ref _logSampler, _samplerFactory(logSamplesPerSecond));
        }

        private void LogDrop(ProbeType probeType, string probeId, [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            if (!_logRateLimiter.ShouldLog(sourceFile, sourceLine, out var skipCount))
            {
                return;
            }

            var probeTypeName = probeType == ProbeType.Snapshot ? "snapshot" : "log";
            const string message = "Global debugger rate limit reached for {ProbeType} probes. Dropping capture for ProbeId={ProbeId}";
            const string messageWithSkipCount = "Global debugger rate limit reached for {ProbeType} probes. Dropping capture for ProbeId={ProbeId}, {SkipCount} additional messages skipped";

            if (skipCount > 0)
            {
                Log.Warning(messageWithSkipCount, probeTypeName, probeId, skipCount);
            }
            else
            {
                Log.Warning(message, probeTypeName, probeId);
            }
        }
    }
}
