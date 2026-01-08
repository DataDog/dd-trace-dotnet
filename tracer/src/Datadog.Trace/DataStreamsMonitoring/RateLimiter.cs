// <copyright file="RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;

namespace Datadog.Trace.DataStreamsMonitoring;

internal sealed class RateLimiter
{
    private readonly object _syncRoot = new();
    private readonly TimeSpan _period = TimeSpan.FromSeconds(30);
    private int _count;
    private DateTime _lastSampleMs;

    /// <summary>
    /// Return what the next call to GetDecision _should_ return (if another thread doesn't sneak first)
    /// AND record that a call was made
    /// </summary>
    public bool PeekDecision()
    {
        Interlocked.Increment(ref _count);
        return DateTime.UtcNow.Subtract(_lastSampleMs) > _period;
    }

    /// <summary>
    /// Return the actual thread safe decision.
    /// If the answer was "true",
    ///  - reset the time until the next "allow".
    ///  - weight will be set to how many times Peek was called i.e. how many events were sampled out.
    /// </summary>
    public bool GetDecision(out int weight)
    {
        var now = DateTime.UtcNow;
        if (now.Subtract(_lastSampleMs) > _period)
        {
            lock (_syncRoot)
            {
                if (now.Subtract(_lastSampleMs) > _period)
                {
                    _lastSampleMs = now;
                    weight = _count;
                    _count = 0;
                    return true;
                }
            }
        }

        weight = 0;
        return false;
    }
}
