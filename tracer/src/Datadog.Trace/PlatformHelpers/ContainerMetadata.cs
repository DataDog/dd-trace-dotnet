// <copyright file="ContainerMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers
{
    /// <summary>
    /// Utility class with methods to interact with container hosts.
    /// </summary>
    internal static class ContainerMetadata
    {
        private const string ControlGroupsFilePath = "/proc/self/cgroup";
        private const string ControlGroupsNamespacesFilePath = "/proc/self/ns/cgroup";
        private const string DefaultControlGroupsMountPath = "/sys/fs/cgroup";
        private const string ContainerRegex = @"[0-9a-f]{64}";
        // The second part is the PCF/Garden regexp. We currently assume no suffix ($) to avoid matching pod UIDs
        // See https://github.com/DataDog/datadog-agent/blob/7.40.x/pkg/util/cgroups/reader.go#L50
        private const string UuidRegex = @"[0-9a-f]{8}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{12}|(?:[0-9a-f]{8}(?:-[0-9a-f]{4}){4}$)";
        private const string TaskRegex = @"[0-9a-f]{32}-\d+";
        private const string ContainerIdRegex = @"(" + UuidRegex + "|" + ContainerRegex + "|" + TaskRegex + @")(?:\.scope)?$";
        private const string CgroupRegex = @"^\d+:([^:]*):(.+)$";

        // From https://github.com/torvalds/linux/blob/5859a2b1991101d6b978f3feb5325dad39421f29/include/linux/proc_ns.h#L41-L49
        // Currently, host namespace inode number are hardcoded, which can be used to detect
        // if we're running in host namespace or not (does not work when running in DinD)
        private const long HostCgroupNamespaceInode = 0xEFFFFFFB;

        private static readonly Lazy<string> ContainerId = new Lazy<string>(GetContainerIdInternal, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly Lazy<string> CgroupInode = new Lazy<string>(GetCgroupInodeInternal, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContainerMetadata));

        /// <summary>
        /// Gets the id of the container executing the code.
        /// Return <c>null</c> if code is not executing inside a supported container.
        /// </summary>
        /// <returns>The container id or <c>null</c>.</returns>
        public static string GetContainerId()
        {
            return ContainerId.Value;
        }

        /// <summary>
        /// Gets the unique identifier of the container executing the code.
        /// Return values may be:
        /// <list type="bullet">
        /// <item>"ci-&lt;containerID&gt;" if the container id is available.</item>
        /// <item>"in-&lt;inode&gt;" if the cgroup node controller's inode is available.
        ///        We use the memory controller on cgroupv1 and the root cgroup on cgroupv2.</item>
        /// <item><c>null</c> if neither are available.</item>
        /// </list>
        /// </summary>
        /// <returns>The entity id or <c>null</c>.</returns>
        public static string GetEntityId()
        {
            if (ContainerId.Value is string containerId)
            {
                return $"ci-{containerId}";
            }
            else if (CgroupInode.Value is string cgroupInode)
            {
                return $"in-{cgroupInode}";
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Uses regular expression to try to extract a container id from the specified string.
        /// </summary>
        /// <param name="lines">Lines of text from a cgroup file.</param>
        /// <returns>The container id if found; otherwise, <c>null</c>.</returns>
        public static string ParseContainerIdFromCgroupLines(IEnumerable<string> lines)
        {
            return lines.Select(ParseContainerIdFromCgroupLine)
                        .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        /// <summary>
        /// Uses regular expression to try to extract a container id from the specified string.
        /// </summary>
        /// <param name="line">A single line from a cgroup file.</param>
        /// <returns>The container id if found; otherwise, <c>null</c>.</returns>
        public static string ParseContainerIdFromCgroupLine(string line)
        {
            var lineMatch = Regex.Match(line, ContainerIdRegex);

            return lineMatch.Success
                       ? lineMatch.Groups[1].Value
                       : null;
        }

        /// <summary>
        /// Uses regular expression to try to extract controller/cgroup-node-path pairs from the specified string
        /// then, using these pairs, will return the first inode found from the concatenated path
        /// <paramref name="controlGroupsMountPath"/>/controller/cgroup-node-path.
        /// If no inode could be found, this will return <c>null</c>.
        /// </summary>
        /// <param name="controlGroupsMountPath">Path to the cgroup mount point.</param>
        /// <param name="lines">Lines of text from a cgroup file.</param>
        /// <returns>The cgroup node controller's inode if found; otherwise, <c>null</c>.</returns>
        public static string ExtractInodeFromCgroupLines(string controlGroupsMountPath, IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var tuple = ParseControllerAndPathFromCgroupLine(line);
                if (tuple is not null
                 && !string.IsNullOrEmpty(tuple.Item2)
                 && (tuple.Item1 == string.Empty || string.Equals(tuple.Item1, "memory", StringComparison.OrdinalIgnoreCase)))
                {
                    string controller = tuple.Item1;
                    string cgroupNodePath = tuple.Item2;
                    var path = Path.Combine(controlGroupsMountPath, controller, cgroupNodePath.TrimStart('/'));

                    if (Directory.Exists(path) && TryGetInode(path, out long output))
                    {
                        return output.ToString();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Uses regular expression to try to extract a controller/cgroup-node-path pair from the specified string.
        /// </summary>
        /// <param name="line">A single line from a cgroup file.</param>
        /// <returns>The controller/cgroup-node-path pair if found; otherwise, <c>null</c>.</returns>
        public static Tuple<string, string> ParseControllerAndPathFromCgroupLine(string line)
        {
            var lineMatch = Regex.Match(line, CgroupRegex);

            return lineMatch.Success
                       ? new(lineMatch.Groups[1].Value, lineMatch.Groups[2].Value)
                       : null;
        }

        internal static bool TryGetInode(string path, out long result)
            => TryGetInodeUsingPInvoke(path, out result)
            || TryGetInodeUsingStat(path, out result);

        // Internal for testing
        internal static bool TryGetInodeUsingPInvoke(string path, out long result)
        {
            result = 0;

            try
            {
                return NativeInterop.GetInode(path, out result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error obtaining inode using PInvoke");
                return false;
            }
        }

        // Internal for testing
        internal static bool TryGetInodeUsingStat(string path, out long result)
        {
            result = 0;

            try
            {
                var statCommand = ProcessHelpers.RunCommand(new ProcessHelpers.Command("stat", $"-c %i {path}"));
                return long.TryParse(statCommand?.Output, out result);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error obtaining inode using stat");
                return false;
            }
        }

        private static string GetContainerIdInternal()
        {
            try
            {
                var isLinux = string.Equals(FrameworkDescription.Instance.OSPlatform, "Linux", StringComparison.OrdinalIgnoreCase);

                if (isLinux &&
                    File.Exists(ControlGroupsFilePath))
                {
                    var lines = File.ReadLines(ControlGroupsFilePath);
                    return ParseContainerIdFromCgroupLines(lines);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading cgroup file. Will not report container id.");
            }

            return null;
        }

        private static string GetCgroupInodeInternal()
        {
            try
            {
                var isLinux = string.Equals(FrameworkDescription.Instance.OSPlatform, "Linux", StringComparison.OrdinalIgnoreCase);
                if (!isLinux)
                {
                    return null;
                }

                // If we're running in the host cgroup namespace, do not get the inode.
                // This would indicate that we're not in a container and the inode we'd
                // return is not related to a container.
                if (IsHostCgroupNamespaceInternal())
                {
                    return null;
                }

                if (File.Exists(ControlGroupsFilePath))
                {
                    var lines = File.ReadLines(ControlGroupsFilePath);
                    return ExtractInodeFromCgroupLines(DefaultControlGroupsMountPath, lines);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading cgroup file. Will not report inode.");
            }

            return null;
        }

        private static bool IsHostCgroupNamespaceInternal()
        {
            return File.Exists(ControlGroupsNamespacesFilePath) && TryGetInode(ControlGroupsNamespacesFilePath, out long output) && output == HostCgroupNamespaceInode;
        }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Keeping the names matching the spec definitions")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Keeping the names matching the spec definitions")]
        private static class NativeInterop
        {
            /// <summary>
            /// Get the inode for the provided path. Does so portably on x64 and arm64 Linux only.
            /// </summary>
            public static bool GetInode(string path, out long inode)
            {
                var arch = RuntimeInformation.ProcessArchitecture;
                // using AT_FDCWD and AT_SYMLINK_NOFOLLOW for lstat-like behaviour
                if (arch == Architecture.X64)
                {
                    if (Native.FstatatX64(Native.AT_FDCWD, path, out var st, Native.AT_SYMLINK_NOFOLLOW) == 0)
                    {
                        inode = (long)st.st_ino;
                        return true;
                    }
                }
                else if (arch == Architecture.Arm64)
                {
                    if (Native.FstatatArm64(Native.AT_FDCWD, path, out var st, Native.AT_SYMLINK_NOFOLLOW) == 0)
                    {
                        inode = (long)st.st_ino;
                        return true;
                    }
                }

                inode = 0;
                return false;
            }

            // https://www.man7.org/linux/man-pages/man3/timespec.3type.html
            [StructLayout(LayoutKind.Sequential)]
            private struct Timespec
            {
                public nint tv_sec; // time_t (64-bit on 64-bit Linux)
                public nint tv_nsec; // long
            }

            // x86_64 (glibc & musl)
            [StructLayout(LayoutKind.Sequential, Pack = 8)]
            private struct StatX64
            {
                public nuint st_dev; // dev_t
                public nuint st_ino; // ino_t
                public nuint st_nlink; // nlink_t (64-bit on x86_64)
                public uint st_mode; // mode_t
                public uint st_uid; // uid_t
                public uint st_gid; // gid_t
                public int __pad0; // matches glibc explicit pad
                public nuint st_rdev; // dev_t
                public long st_size; // off_t
                public long st_blksize; // blksize_t (64-bit on x86_64)
                public long st_blocks; // blkcnt_t
                public Timespec st_atim;
                public Timespec st_mtim;
                public Timespec st_ctim;
                public long __glibc_reserved0;
                public long __glibc_reserved1;
                public long __glibc_reserved2;
            }

            // aarch64/arm64 (glibc & musl)
            // NOTE: nlink_t and blksize_t are 32-bit here; there is extra padding.
            [StructLayout(LayoutKind.Sequential, Pack = 8)]
            private struct StatArm64
            {
                public nuint st_dev; // dev_t (8)
                public nuint st_ino; // ino_t (8)
                public uint st_mode; // mode_t (4)
                public uint st_nlink; // nlink_t (4)  <-- differs from x86_64
                public uint st_uid; // uid_t (4)
                public uint st_gid; // gid_t (4)
                public nuint st_rdev; // dev_t (8)
                public nuint __pad; // padding; glibc names it __pad1, musl uses unsigned long
                public long st_size; // off_t (8)
                public int st_blksize; // blksize_t (4)  <-- differs from x86_64
                public int __pad2; // padding
                public long st_blocks; // blkcnt_t (8)
                public Timespec st_atim;
                public Timespec st_mtim;
                public Timespec st_ctim;
                public int __reserved0; // glibc: int[2], musl: unsigned[2]
                public int __reserved1;
            }

            // P/Invoke: use the libc wrappers (not raw syscalls)
            private static class Native
            {
                private const string Lib = "libc";
                public const int AT_FDCWD = -100;
                public const int AT_SYMLINK_NOFOLLOW = 0x100;

                // Path-based
                [DllImport(Lib, EntryPoint = "fstatat", SetLastError = true)]
                public static extern int FstatatX64(int dirfd, string path, out StatX64 buf, int flags);

                [DllImport(Lib, EntryPoint = "fstatat", SetLastError = true)]
                public static extern int FstatatArm64(int dirfd, string path, out StatArm64 buf, int flags);
            }
        }
    }
}
#endif
