// <copyright file="DefaultMemoryChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Caching;

internal sealed class DefaultMemoryChecker : IMemoryChecker
{
    private const long LowMemoryThreshold = 1_073_741_824; // 1 GB in bytes

    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DefaultMemoryChecker>();

    private DefaultMemoryChecker()
    {
        IsLowResourceEnvironment = CheckLowResourceEnvironment();
    }

    internal static DefaultMemoryChecker Instance { get; } = new();

    public bool IsLowResourceEnvironment { get; }

    private bool CheckLowResourceEnvironment()
    {
        try
        {
            // Check if we're using more than 75% of available memory or there is less than 1GB of RAM available.
            return IsLowResourceEnvironmentGc() || IsLowResourceEnvironmentSystem();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to check if environment is low on resources. Assuming it is not.");
            return false;
        }
    }

    private bool IsLowResourceEnvironmentGc()
    {
#if NETCOREAPP3_1_OR_GREATER

        long totalMemory = GC.GetTotalMemory(false);
        long totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        // Check if we're using more than 75% of available memory.
        return totalMemory > (totalAvailableMemory * 0.75);
#else
        return false;
#endif
    }

    private bool IsLowResourceEnvironmentSystem()
    {
        return FrameworkDescription.Instance.OSPlatform switch
        {
            OSPlatformName.Windows => CheckWindowsMemory(),
            OSPlatformName.Linux => CheckUnixMemory(),
            _ => false,
        };
    }

    internal bool CheckWindowsMemory()
    {
        try
        {
            if (WindowsMemoryInfo.TryGetAvailablePhysicalMemory(out var availableMemory))
            {
                // If less than 1GB of RAM is available, consider it a low-resource environment
                return availableMemory < 1_073_741_824; // 1 GB in bytes
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Fail to call GlobalMemoryStatusEx");

            // If we can't access memory info, we'll fall back to the default capacity
            throw;
        }

        Logger.Debug("Fail to get windows available memory info");
        return false;
    }

    internal bool CheckUnixMemory()
    {
        try
        {
            // for linux we can check /proc/meminfo
            var memAvailable = ReadMemInfo();
            if (memAvailable.IsEmpty)
            {
                Logger.Debug("Fail to get unix available memory info");
                return false;
            }

            var asBytes = MemoryMarshal.AsBytes(memAvailable);
            Utf8Parser.TryParse(asBytes, out long availableKb, out var bytes);
            if (bytes > 0 && availableKb > 0)
            {
                return availableKb * 1024 < LowMemoryThreshold;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Fail to read or parse /proc/meminfo");

            // If we can't read memory info, we'll fall back to the default capacity
            throw;
        }

        Logger.Debug("Fail to get unix available memory info");
        return false;
    }

    private ReadOnlySpan<char> ReadMemInfo()
    {
        var empty = ReadOnlySpan<char>.Empty;
        var memInfo = System.IO.File.ReadAllText("/proc/meminfo").AsSpan();
        int startIndex = memInfo.IndexOf("MemAvailable:".AsSpan());
        if (startIndex == -1)
        {
            return empty;
        }

        var line = memInfo.Slice(startIndex);
        int colonIndex = line.IndexOf(':');
        if (colonIndex == -1)
        {
            return empty;
        }

        var value = line.Slice(colonIndex + 1).TrimStart();
        int spaceIndex = value.IndexOf(' ');
        if (spaceIndex == -1)
        {
            return empty;
        }

        return value.Slice(0, spaceIndex);
    }
}
