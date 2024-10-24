// <copyright file="GlobalMemoryCircuitBreaker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal class GlobalMemoryCircuitBreaker
    {
        private static readonly Lazy<GlobalMemoryCircuitBreaker> _lazyInstance =
            new Lazy<GlobalMemoryCircuitBreaker>(() => new GlobalMemoryCircuitBreaker(
                                                     memoryThreshold: MemoryInfoRetriever.GetDynamicMemoryThreshold(0.5), // 50% of physical memory
                                                     cooldownPeriod: TimeSpan.FromSeconds(10),
                                                     allocationThresholdPercentage: 0.1 // 10% default
                                                 ));

        internal static GlobalMemoryCircuitBreaker Instance => _lazyInstance.Value;

        private readonly MemoryCircuitBreaker _circuitBreaker;

        private GlobalMemoryCircuitBreaker(long memoryThreshold, TimeSpan cooldownPeriod, double allocationThresholdPercentage)
        {
            _circuitBreaker = new MemoryCircuitBreaker(memoryThreshold, cooldownPeriod, allocationThresholdPercentage);
        }

        internal bool CanAllocate(long estimatedAllocationSize = 0)
        {
            return _circuitBreaker.CanAllocate(estimatedAllocationSize);
        }

        internal long GetCurrentMemoryUsage() => _circuitBreaker.GetCurrentMemoryUsage();

        internal long GetMemoryThreshold() => _circuitBreaker.GetMemoryThreshold();

        internal double GetAvailableMemoryPercentage() => _circuitBreaker.GetAvailableMemoryPercentage();
    }
}
