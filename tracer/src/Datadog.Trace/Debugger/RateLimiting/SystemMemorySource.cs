// <copyright file="SystemMemorySource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Reads real GC and system memory information. These methods match the
    /// <see cref="TryReadGen2CollectionCount"/> / <see cref="TryReadMemoryUsageRatio"/> delegate shapes
    /// so they can be used directly as the production sources for <see cref="MemoryPressureMonitor"/>.
    /// </summary>
    internal static class SystemMemorySource
    {
#if NETCOREAPP3_1_OR_GREATER || NETFRAMEWORK
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SystemMemorySource));
#endif

        public static bool TryGetGen2CollectionCount(out int count)
        {
            count = GC.CollectionCount(2);
            return true;
        }

        // Returns system/container-wide memory load, not process-private bytes: the container OOMs on total
        // usage, so that is the right scope for "back off allocating". Both paths report fraction-of-available.
        // Future (heavier) option: switch both to processWorkingSet / explicit limit (cgroup/job-object).
        public static bool TryGetMemoryUsageRatio(out double ratio)
        {
            ratio = 0;
#if NETCOREAPP3_1_OR_GREATER
            try
            {
                // Fraction of available memory in use (container/cgroup-aware), not the GC high-load threshold,
                // which is incomparable to the Framework path.
                var gcInfo = GC.GetGCMemoryInfo();
                if (gcInfo.TotalAvailableMemoryBytes > 0)
                {
                    ratio = (double)gcInfo.MemoryLoadBytes / gcInfo.TotalAvailableMemoryBytes;
                    return true;
                }

                Log.Debug("GC.GetGCMemoryInfo returned non-positive TotalAvailableMemoryBytes. Treating memory usage ratio as unsupported on this runtime.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get GC memory info");
            }
#elif NETFRAMEWORK
            try
            {
                // Machine-wide load: GlobalMemoryStatusEx is not container/job-object aware, so it under-reports
                // pressure inside memory-limited Windows containers (sees the host, not the limit). Clamped to [0,1].
                if (WindowsMemoryInfo.TryGetMemoryLoadRatio(out ratio))
                {
                    return true;
                }

                Log.Debug("GlobalMemoryStatusEx did not return memory information. Treating memory usage ratio as unsupported on this platform.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get system memory status");
            }
#else
#endif
            return false;
        }
    }
}
