// <copyright file="TotalProcessorCount.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.RuntimeMetrics;

internal static class TotalProcessorCount
{
    private static readonly Lazy<int?> LazyValue = new(GetTotalProcessorCount);
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TotalProcessorCount));

    /// <summary>
    /// Gets the total number of logical processors on the host machine. Differs from
    /// <see cref="Environment.ProcessorCount"/> (which includes cgroup/container CPU limits
    /// and process affinity). Mirrors the GC's own GCToOSInterface::GetTotalProcessorCount()
    /// </summary>
    public static int? Value => LazyValue.Value;

    [TestingAndPrivateOnly]
    internal static int? GetTotalProcessorCount()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsProcessorCount.GetTotalProcessorCount();
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxProcessorCount.GetTotalProcessorCount();
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacOsProcessorCount.GetTotalProcessorCount();
        }

        return null;
    }

    internal static class WindowsProcessorCount
    {
        private const ushort AllProcessorGroups = 0xFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetActiveProcessorCount(ushort groupNumber);

        /// <summary>
        /// Gets the total number of logical processors on a Windows host via <c>GetActiveProcessorCount</c>,
        /// counting active processors across all processor groups and ignoring process affinity and Job Object
        /// CPU limits (the container-CPU-cap mechanism on Windows).
        /// </summary>
        internal static int? GetTotalProcessorCount()
        {
            var result = GetActiveProcessorCount(AllProcessorGroups);
            if (result > 0)
            {
                return result;
            }

            var error = Marshal.GetLastWin32Error();
            Log.Warning(
                "GetActiveProcessorCount failed when getting total machine processor count. ErrorCode={ErrorCode}",
                property: error);
            return null;
        }
    }

    internal static class LinuxProcessorCount
    {
        private const string OnlineCpusPath = "/sys/devices/system/cpu/online";

        /// <summary>
        /// Gets the total number of logical processors on a Linux host by reading the online-CPU range
        /// reported by the kernel, the same source <c>sysconf(_SC_NPROCESSORS_ONLN)</c> itself reads, and unaffected
        /// by cgroup CPU quotas. Avoids P/Invoking into libc, which fails to resolve on musl-based images (e.g. Alpine).
        /// </summary>
        internal static int? GetTotalProcessorCount()
        {
            try
            {
                var contents = File.ReadAllText(OnlineCpusPath);
                return TryParseOnlineCpuRanges(contents.AsSpan());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading {Path} to determine total machine processor count", OnlineCpusPath);
                return null;
            }
        }

        // Parses the Linux cpu-list-format (see Documentation/admin-guide/kernel-parameters.txt)
        // comma-separated list of either a single CPU index ("0") or an inclusive range ("0-7"), e.g. "0-3,8-11".
        [TestingAndPrivateOnly]
        internal static int? TryParseOnlineCpuRanges(ReadOnlySpan<char> contents)
        {
            var trimmed = contents.Trim();
            if (trimmed.IsEmpty)
            {
                return null;
            }

            var count = 0;
            var remaining = trimmed;
            while (!remaining.IsEmpty)
            {
                var commaIndex = remaining.IndexOf(',');
                var token = commaIndex < 0 ? remaining : remaining[..commaIndex];

                if (!TryParseToken(token, out var tokenCount))
                {
                    return null;
                }

                count += tokenCount;

                if (commaIndex < 0)
                {
                    break;
                }

                remaining = remaining[(commaIndex + 1)..];
                if (remaining.IsEmpty)
                {
                    // trailing comma with no following token
                    return null;
                }
            }

            return count > 0 ? count : null;

            static bool TryParseToken(ReadOnlySpan<char> token, out int tokenCount)
            {
                tokenCount = 0;

                var dashIndex = token.IndexOf('-');
                if (dashIndex < 0)
                {
                    if (!int.TryParse(token, out var single) || single < 0)
                    {
                        return false;
                    }

                    tokenCount = 1;
                    return true;
                }

                var startSpan = token[..dashIndex];
                var endSpan = token[(dashIndex + 1)..];

                if (!int.TryParse(startSpan, out var start) || start < 0 ||
                    !int.TryParse(endSpan, out var end) || end < start)
                {
                    return false;
                }

                tokenCount = end - start + 1;
                return true;
            }
        }
    }

    internal static class MacOsProcessorCount
    {
        private const string LogicalCpuName = "hw.logicalcpu";

        [DllImport("libSystem.dylib", EntryPoint = "sysctlbyname", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int SysCtlByName(string name, out int oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

        /// <summary>
        /// Gets the total number of logical processors on a macOS host via <c>sysctlbyname("hw.logicalcpu", ...)</c>,
        /// the standard, stable way to query total logical CPUs on macOS.
        /// </summary>
        internal static int? GetTotalProcessorCount()
        {
            var size = new IntPtr(sizeof(int));
            var result = SysCtlByName(LogicalCpuName, out var value, ref size, IntPtr.Zero, IntPtr.Zero);
            if (result == 0 && value > 0)
            {
                return value;
            }

            var error = Marshal.GetLastWin32Error();
            Log.Warning(
                "sysctlbyname failed when getting total machine processor count. ErrorCode={ErrorCode}",
                property: error);
            return null;
        }
    }
}
#endif
