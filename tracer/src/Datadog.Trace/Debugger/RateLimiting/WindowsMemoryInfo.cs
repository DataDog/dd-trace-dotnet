// <copyright file="WindowsMemoryInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal static class WindowsMemoryInfo
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WindowsMemoryInfo));

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        internal static bool TryGetMemoryLoadRatio(out double ratio)
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref status))
            {
                ratio = Math.Min(1.0, status.dwMemoryLoad / 100.0);
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            Log.Debug(
                "GlobalMemoryStatusEx failed when getting memory load ratio. ErrorCode={ErrorCode}",
                property: error);
            ratio = 0;
            return false;
        }

        internal static bool TryGetAvailablePhysicalMemory(out ulong bytes)
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref status))
            {
                bytes = status.ullAvailPhys;
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            Log.Debug(
                "GlobalMemoryStatusEx failed when getting available physical memory. ErrorCode={ErrorCode}",
                property: error);
            bytes = 0;
            return false;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
#pragma warning disable SA1307
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
#pragma warning restore SA1307
        }
    }
}
