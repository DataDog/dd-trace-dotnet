// <copyright file="FakeClock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Debugger.RateLimiting;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    /// <summary>
    /// Fake clock for deterministic time testing without real time dependencies
    /// </summary>
    internal sealed class FakeClock(double? frequency = null) : IHighResolutionClock
    {
        private readonly Queue<long> _timestamps = new();
        private long _currentTimestamp = 0;

        public double Frequency { get; } = frequency ?? Stopwatch.Frequency;

        public FakeClock WithTicksAtSeconds(params double[] seconds)
        {
            foreach (var second in seconds)
            {
                _timestamps.Enqueue((long)(second * this.Frequency));
            }

            return this;
        }

        public long GetTimestamp()
        {
            if (_timestamps.Count > 0)
            {
                _currentTimestamp = _timestamps.Dequeue();
            }

            return _currentTimestamp;
        }
    }
}
