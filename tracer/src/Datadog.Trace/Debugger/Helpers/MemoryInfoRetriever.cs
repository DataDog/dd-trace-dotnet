// <copyright file="MemoryInfoRetriever.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class MemoryInfoRetriever
    {
        private const long DEFAULT_MEMORY_THRESHOLD = 1024L * 1024 * 1024; // 1 GB
        private const double DEFAULT_PERCENTAGE = 0.7;

        public static long GetTotalPhysicalMemory()
        {
            if (IsRunningInContainer())
            {
                return GetContainerMemoryLimit();
            }
            else if (IsWindows())
            {
                return GetWindowsTotalPhysicalMemory();
            }
            else if (IsLinux())
            {
                return GetLinuxTotalPhysicalMemory();
            }
            else if (IsMacOS())
            {
                return GetMacOSTotalPhysicalMemory();
            }

            return 0; // Indicates failure to retrieve memory info
        }

        internal static long GetDynamicMemoryThreshold(double percentageOfPhysicalMemory = DEFAULT_PERCENTAGE)
        {
            long totalPhysicalMemory = GetTotalPhysicalMemory();

            if (totalPhysicalMemory > 0)
            {
                return (long)(totalPhysicalMemory * percentageOfPhysicalMemory);
            }
            else
            {
                // Fallback to default values based on the environment
                if (IsWindows())
                {
                    return 2L * 1024 * 1024 * 1024; // 2 GB for Windows
                }
                else if (IsLinux())
                {
                    return 1024L * 1024 * 1024; // 1 GB for Linux
                }
                else if (IsMacOS())
                {
                    return 2L * 1024 * 1024 * 1024; // 2 GB for macOS
                }
                else
                {
                    return DEFAULT_MEMORY_THRESHOLD;
                }
            }
        }

        private static bool IsRunningInContainer()
        {
            return File.Exists("/.dockerenv") || File.Exists("/run/.containerenv");
        }

        private static bool IsWindows()
        {
#if NETCOREAPP3_0_OR_GREATER
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
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
                string cgroupMemLimitPath = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
                if (File.Exists(cgroupMemLimitPath))
                {
                    string memLimitStr = File.ReadAllText(cgroupMemLimitPath).Trim();
                    if (long.TryParse(memLimitStr, out long memLimit))
                    {
                        return memLimit;
                    }
                }
            }
            catch
            {
                // Silently handle any exceptions
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
                // Silently handle any exceptions
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
                // Silently handle any exceptions
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
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result.Trim();
        }

        // Windows-specific structures and methods
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    }
}
