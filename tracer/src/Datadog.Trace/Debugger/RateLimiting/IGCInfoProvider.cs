// <copyright file="IGCInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Abstraction for GC information
    /// </summary>
    internal interface IGCInfoProvider
    {
        /// <summary>
        /// Gets the number of times garbage collection has occurred for gen 2 objects
        /// </summary>
        int GetGen2CollectionCount();

#if NETCOREAPP3_1_OR_GREATER
        /// <summary>
        /// Gets memory info from the garbage collector
        /// </summary>
        GCMemoryInfo GetGCMemoryInfo();
#endif

        /// <summary>
        /// Gets the memory usage ratio (0.0 to 1.0+)
        /// </summary>
        double GetMemoryUsageRatio();
    }
}
