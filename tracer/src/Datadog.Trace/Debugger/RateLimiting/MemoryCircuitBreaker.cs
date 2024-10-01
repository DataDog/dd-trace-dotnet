// <copyright file="MemoryCircuitBreaker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Threading;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal class MemoryCircuitBreaker
    {
        private readonly long _memoryThreshold;
        private readonly TimeSpan _cooldownPeriod;
        private readonly double _allocationThresholdPercentage;
        private long _lastTrippedTicks = 0;
        private long _currentMemoryUsage = 0;

        internal MemoryCircuitBreaker(
            long memoryThreshold = 1024L * 1024 * 1024, // 1 GB default
            TimeSpan? cooldownPeriod = null,
            double allocationThresholdPercentage = 0.05) // 5% default
        {
            _memoryThreshold = memoryThreshold;
            _cooldownPeriod = cooldownPeriod ?? TimeSpan.FromSeconds(10);
            _allocationThresholdPercentage = allocationThresholdPercentage;
        }

        public bool CanAllocate(long estimatedAllocationSize = 0)
        {
            long currentTicks = DateTime.UtcNow.Ticks;
            long lastTrippedTicks = Interlocked.Read(ref _lastTrippedTicks);

            if ((new TimeSpan(currentTicks - lastTrippedTicks)) < _cooldownPeriod)
            {
                return false;
            }

            _currentMemoryUsage = GC.GetTotalMemory(false);

            if (_currentMemoryUsage > _memoryThreshold)
            {
                Interlocked.Exchange(ref _lastTrippedTicks, currentTicks);
                return false;
            }

            if (estimatedAllocationSize > 0)
            {
                long availableMemory = _memoryThreshold - _currentMemoryUsage;
                if (estimatedAllocationSize > availableMemory * _allocationThresholdPercentage)
                {
                    Interlocked.Exchange(ref _lastTrippedTicks, currentTicks);
                    return false;
                }
            }

            return true;
        }

        internal long GetCurrentMemoryUsage() => _currentMemoryUsage;

        internal long GetMemoryThreshold() => _memoryThreshold;

        internal double GetAvailableMemoryPercentage() =>
            (double)(_memoryThreshold - _currentMemoryUsage) / _memoryThreshold * 100;
    }
}
