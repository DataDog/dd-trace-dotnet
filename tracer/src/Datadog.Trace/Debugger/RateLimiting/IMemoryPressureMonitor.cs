// <copyright file="IMemoryPressureMonitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Monitors runtime memory pressure to protect against OOM scenarios.
    /// </summary>
    internal interface IMemoryPressureMonitor : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether memory pressure is currently high
        /// </summary>
        bool IsHighMemoryPressure { get; }

        /// <summary>
        /// Gets current memory usage as percentage (0-100)
        /// </summary>
        double MemoryUsagePercent { get; }

        /// <summary>
        /// Gets Gen2 collections per second (indicator of pressure)
        /// </summary>
        double Gen2CollectionsPerSecond { get; }

        /// <summary>
        /// Gets the high pressure memory threshold as a ratio (0-1)
        /// </summary>
        double HighPressureThreshold { get; }

        /// <summary>
        /// Gets the maximum Gen2 collections per second threshold
        /// </summary>
        int MaxGen2PerSecond { get; }

        /// <summary>
        /// Records memory pressure event
        /// </summary>
        void RecordMemoryPressureEvent();
    }
}
