// <copyright file="SystemGCInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
#if NETFRAMEWORK
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.RateLimiting;

#endif
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Default implementation that uses real GC and system memory APIs
    /// </summary>
    internal sealed class SystemGCInfoProvider : IGCInfoProvider
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SystemGCInfoProvider>();

        public int GetGen2CollectionCount()
        {
            return GC.CollectionCount(2);
        }

#if NETCOREAPP3_1_OR_GREATER
        public GCMemoryInfo GetGCMemoryInfo()
        {
            return GC.GetGCMemoryInfo();
        }
#endif

        public double GetMemoryUsageRatio()
        {
            double memoryUsageRatio = 0;
#if NETCOREAPP3_1_OR_GREATER
            try
            {
                var gcInfo = GetGCMemoryInfo();
                if (gcInfo.HighMemoryLoadThresholdBytes > 0)
                {
                    memoryUsageRatio = (double)gcInfo.MemoryLoadBytes / gcInfo.HighMemoryLoadThresholdBytes;
                }
                else
                {
                    Log.Debug(
                        "GC.GetGCMemoryInfo returned non-positive HighMemoryLoadThresholdBytes. Treating memory usage ratio as unsupported on this runtime.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get GC memory info");
            }
#elif NETFRAMEWORK
            try
            {
                if (WindowsMemoryInfo.TryGetMemoryLoadRatio(out var ratio))
                {
                    // Windows GlobalMemoryStatusEx returns dwMemoryLoad (0-100) which is clamped
                    // to [0,1] range. This differs from .NET Core GC path which can exceed 1.0.
                    memoryUsageRatio = ratio;
                }
                else
                {
                    Log.Debug(
                        "GlobalMemoryStatusEx did not return memory information. Treating memory usage ratio as unsupported on this platform.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get system memory status");
            }
#else
            Log.Debug(
                "Memory usage ratio is not supported on this runtime target. Not NETCOREAPP3_1_OR_GREATER or NETFRAMEWORK; defaulting to 0.");
            memoryUsageRatio = 0;
#endif
            return memoryUsageRatio;
        }
    }
}
