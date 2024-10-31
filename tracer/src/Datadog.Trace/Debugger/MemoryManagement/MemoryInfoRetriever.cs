// <copyright file="MemoryInfoRetriever.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Debugger.MemoryManagement
{
    internal static class MemoryInfoRetriever
    {
        private const long DefaultMemoryThreshold = 1024L * 1024 * 1024; // 1 GB
        private const double DefaultPercentage = 0.7;

        public static long GetTotalPhysicalMemory()
        {
            if (IsRunningInContainer())
            {
                return GetContainerMemoryLimit();
            }

            if (IsWindows())
            {
                return GetWindowsTotalPhysicalMemory();
            }

            if (IsLinux())
            {
                return GetLinuxTotalPhysicalMemory();
            }

            if (IsMacOS())
            {
                return GetMacOSTotalPhysicalMemory();
            }

            return 0; // fail to retrieve memory info
        }

        internal static long GetDynamicMemoryThreshold(double percentageOfPhysicalMemory = DefaultPercentage)
        {
            long totalPhysicalMemory = GetTotalPhysicalMemory();

            if (totalPhysicalMemory > 0)
            {
                return (long)(totalPhysicalMemory * percentageOfPhysicalMemory);
            }

            // Fallback to default values based on the environment
            if (IsWindows())
            {
                return 2L * 1024 * 1024 * 1024; // 2 GB for Windows
            }

            if (IsLinux())
            {
                return 1024L * 1024 * 1024; // 1 GB for Linux
            }

            if (IsMacOS())
            {
                return 2L * 1024 * 1024 * 1024; // 2 GB for macOS
            }

            return DefaultMemoryThreshold;
        }

        private static bool IsRunningInContainer()
        {
            return File.Exists("/.dockerenv") || File.Exists("/run/.containerenv");
        }

        private static bool IsWindows()
        {
#if NETCOREAPP3_0_OR_GREATER
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
        }

        private static bool IsLinux()
        {
#if NETCOREAPP3_0_OR_GREATER
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#else
            return Environment.OSVersion.Platform == PlatformID.Unix && !IsMacOS();
#endif
        }

        private static bool IsMacOS()
        {
#if NETCOREAPP3_0_OR_GREATER
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#else
            return Environment.OSVersion.Platform == PlatformID.Unix &&
                   Directory.Exists("/System/Library/CoreServices");
#endif
        }

        private static long GetContainerMemoryLimit()
        {
            try
            {
                const string cgroupMemLimitPath = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
                if (File.Exists(cgroupMemLimitPath))
                {
                    var memLimitStr = File.ReadAllText(cgroupMemLimitPath).Trim();
                    if (long.TryParse(memLimitStr, out long memLimit))
                    {
                        return memLimit;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return 0; // Indicates failure to retrieve container memory limit
        }

        private static long GetWindowsTotalPhysicalMemory()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return (long)memStatus.ullTotalPhys;
                }
            }
            catch
            {
                // ignored
            }

            return 0;
        }

        private static long GetLinuxTotalPhysicalMemory()
        {
            try
            {
                string[] lines = File.ReadAllLines("/proc/meminfo");
                foreach (string line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out long memKb))
                        {
                            return memKb * 1024; // Convert KB to bytes
                        }
                    }
                }
            }
            catch
            {
                // Silently handle any exceptions
            }

            return 0;
        }

        private static long GetMacOSTotalPhysicalMemory()
        {
            try
            {
                var output = ExecuteCommand("sysctl", "-n hw.memsize");
                if (long.TryParse(output, out long memSize))
                {
                    return memSize;
                }
            }
            catch
            {
                // ignored
            }

            return 0;
        }

        private static string ExecuteCommand(string command, string arguments)
        {
            var process = new System.Diagnostics.Process()
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            var result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result.Trim();
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401 // Fields should be private
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }
    }
}
