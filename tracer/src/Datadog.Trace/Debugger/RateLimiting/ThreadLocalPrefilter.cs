// <copyright file="ThreadLocalPrefilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Ultra-cheap thread-local prefilter that rejects most requests under high load
    /// using a simple counter and mask. Much faster than NextDouble() or atomics.
    ///
    /// Thread Safety: Each thread has its own counter (ThreadStatic). The global mask
    /// is volatile and can be updated from any thread safely.
    ///
    /// Performance: ~2ns per check (TLS read + increment + bitwise AND + branch).
    /// No atomics, no allocations, no contention.
    ///
    /// Integer Overflow: Counter will overflow from int.MaxValue to int.MinValue.
    /// This is safe because bitwise AND works correctly with two's complement representation.
    /// The filtering pattern repeats every 2^32 calls per thread, which is acceptable.
    /// </summary>
    internal static class ThreadLocalPrefilter
    {
        [ThreadStatic]
        private static int _counter;

        private static volatile int _globalMask = 0; // 0 means no filtering

        /// <summary>
        /// Sets the global filter mask. Higher values = more aggressive filtering.
        /// Mask must be 2^n - 1 (e.g., 0, 1, 3, 7, 15, 31, 63, 127, 255) for proper distribution.
        ///
        /// Mask of 0  = no filtering (100% allowed)
        /// Mask of 1  = filter 50% (1 in 2 allowed)
        /// Mask of 3  = filter 75% (1 in 4 allowed)
        /// Mask of 7  = filter 87.5% (1 in 8 allowed)
        /// Mask of 15 = filter 93.75% (1 in 16 allowed)
        /// Mask of 31 = filter 96.875% (1 in 32 allowed)
        /// Mask of 63 = filter 98.4375% (1 in 64 allowed)
        /// </summary>
        /// <param name="mask">The mask value (should be 2^n - 1)</param>
        public static void SetFilterMask(int mask)
        {
            if (mask < 0)
            {
                mask = 0; // Negative values treated as no filtering
            }

            _globalMask = mask;
        }

        /// <summary>
        /// Gets the current filter mask
        /// </summary>
        public static int GetFilterMask()
        {
            return _globalMask;
        }

        /// <summary>
        /// Ultra-fast check: returns true if request should be allowed to proceed.
        /// This is the first gate - if it returns false, skip all other checks.
        ///
        /// Implementation: Uses thread-local counter that increments on each call.
        /// The counter is ANDed with the global mask - if result is 0, allow the request.
        /// This distributes rejections evenly across calls.
        ///
        /// Example with mask=3 (binary: 11):
        ///   Counter=0 (binary: ...0000) &amp; 3 = 0 → Allow
        ///   Counter=1 (binary: ...0001) &amp; 3 = 1 → Reject
        ///   Counter=2 (binary: ...0010) &amp; 3 = 2 → Reject
        ///   Counter=3 (binary: ...0011) &amp; 3 = 3 → Reject
        ///   Counter=4 (binary: ...0100) &amp; 3 = 0 → Allow (pattern repeats)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldAllow()
        {
            var mask = _globalMask; // single volatile read
            if (mask == 0)
            {
                return true; // No filtering
            }

            // Increment thread-local counter and check against mask
            // This is essentially free compared to any other operation (~2ns)
            // Integer overflow is safe - wraps around and filtering pattern continues
            unchecked
            {
                return ((++_counter) & mask) == 0;
            }
        }

        /// <summary>
        /// Adjusts filter mask based on global pressure.
        /// Call this periodically (e.g., from global budget window reset).
        /// Thread-safe - can be called from any thread.
        /// </summary>
        /// <param name="globalUsagePercentage">Current global CPU usage percentage (0-100+)</param>
        /// <param name="isExhausted">Whether the global budget is currently exhausted</param>
        public static void AdjustForPressure(double globalUsagePercentage, bool isExhausted)
        {
            if (isExhausted || globalUsagePercentage > 90)
            {
                // Severe pressure: filter 93.75% (let only 1 in 16 through)
                SetFilterMask(15);
            }
            else if (globalUsagePercentage > 75)
            {
                // High pressure: filter 87.5% (let only 1 in 8 through)
                SetFilterMask(7);
            }
            else if (globalUsagePercentage > 50)
            {
                // Medium pressure: filter 75% (let only 1 in 4 through)
                SetFilterMask(3);
            }
            else if (globalUsagePercentage > 25)
            {
                // Low pressure: filter 50% (let 1 in 2 through)
                SetFilterMask(1);
            }
            else
            {
                // No pressure: no filtering
                SetFilterMask(0);
            }
        }
    }
}
