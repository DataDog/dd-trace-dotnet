// <copyright file="DefaultMemoryChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Caching;

internal class DefaultMemoryChecker : IMemoryChecker
{
    private const long LowMemoryThreshold = 1_073_741_824; // 1 GB in bytes

    private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DefaultMemoryChecker>();

    private static readonly Lazy<DefaultMemoryChecker> _instance = new Lazy<DefaultMemoryChecker>(() => new DefaultMemoryChecker(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly bool _isLowResourceEnvironment;

    private DefaultMemoryChecker()
    {
        _isLowResourceEnvironment = CheckLowResourceEnvironment();
    }

    public static DefaultMemoryChecker Instance => _instance.Value;

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public bool IsLowResourceEnvironment()
    {
        return _isLowResourceEnvironment;
    }

    private bool CheckLowResourceEnvironment()
    {
        try
        {
            Logger.Information("Checking if environment is low on resources");
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
#if NETCOREAPP3_0_OR_GREATER

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
        return FrameworkDescription.Instance.IsWindows() ? CheckWindowsMemory() : CheckUnixMemory();
    }

    internal bool CheckWindowsMemory()
    {
        try
        {
            if (MEMORYSTATUSEX.GetAvailablePhysicalMemory(out var availableMemory))
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

        return false;
    }

    internal bool CheckUnixMemory()
    {
        try
        {
            // for linux we can check /proc/meminfo
            var memAvailable = ReadMemInfo();
            if (long.TryParse(memAvailable, out long availableKB))
            {
                return availableKB * 1024 < LowMemoryThreshold;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Fail to read or parse /proc/meminfo");

            // If we can't read memory info, we'll fall back to the default capacity
            throw;
        }

        return false;
    }

    protected virtual string ReadMemInfo()
    {
        string memInfo = System.IO.File.ReadAllText("/proc/meminfo");
        var memAvailable = memInfo.Split('\n')
                                  .FirstOrDefault(l => l.StartsWith("MemAvailable:"))
                                 ?.Split(':')[1].Trim().Split(' ')[0];
        return memAvailable;
    }

    // Windows API for memory information
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0169 // Field is never used
        private uint dwLength;
        private uint dwMemoryLoad;
        private ulong ullTotalPhys;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        private ulong ullAvailPhys;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
        private ulong ullTotalPageFile;
        private ulong ullAvailPageFile;
        private ulong ullTotalVirtual;
        private ulong ullAvailVirtual;
        private ulong ullAvailExtendedVirtual;
#pragma warning restore CS0169 // Field is never used
#pragma warning restore IDE0044 // Add readonly modifier

        private MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }

        internal static bool GetAvailablePhysicalMemory(out ulong availableMemory)
        {
            availableMemory = 0;
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                availableMemory = memStatus.ullAvailPhys;
                return true;
            }

            return false;
        }
    }
}
