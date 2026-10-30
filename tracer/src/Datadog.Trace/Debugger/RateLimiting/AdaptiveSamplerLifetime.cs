// <copyright file="AdaptiveSamplerLifetime.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal static class AdaptiveSamplerLifetime
    {
        private const int AverageLookback = 180;
        private const int BudgetLookback = 16;

        private static readonly TimeSpan WindowDuration = TimeSpan.FromSeconds(1);

        public static IAdaptiveSampler Create(double snapshotsPerSecond)
        {
            // The one-second window makes samples per window equal snapshots per second. SetRate then picks window
            // sampling for rates of 1 or more and interval sampling for rates between 0 and 1.
            var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 0, AverageLookback, BudgetLookback, rollWindowCallback: null);
            sampler.SetRate(snapshotsPerSecond);
            return sampler;
        }

        public static void Replace(ref IAdaptiveSampler sampler, IAdaptiveSampler replacement)
        {
            Dispose(Interlocked.Exchange(ref sampler, replacement));
        }

        public static void Dispose(ref IAdaptiveSampler sampler)
        {
            Dispose(Interlocked.Exchange(ref sampler, NopAdaptiveSampler.Instance));
        }

        public static void Dispose(IAdaptiveSampler sampler)
        {
            if (!ReferenceEquals(sampler, NopAdaptiveSampler.Instance))
            {
                sampler.Dispose();
            }
        }
    }
}
