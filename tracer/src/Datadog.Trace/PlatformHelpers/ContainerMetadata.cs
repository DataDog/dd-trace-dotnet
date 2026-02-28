// <copyright file="ContainerMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers
{
    /// <summary>
    /// Utility class with methods to interact with container hosts.
    /// </summary>
    internal sealed class ContainerMetadata
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

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContainerMetadata));

        public static readonly ContainerMetadata Instance = new();

        private readonly Lazy<string?> _containerId;
        private readonly Lazy<string?> _entityId;

        private ContainerMetadata()
        {
            _containerId = new Lazy<string?>(GetContainerIdInternal, LazyThreadSafetyMode.ExecutionAndPublication);
            _entityId = new Lazy<string?>(() => GetEntityIdInternal(_containerId), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        // For use in tests only
        [TestingOnly]
        public ContainerMetadata(string? containerId, string? entityId)
        {
            _containerId = new Lazy<string?>(() => containerId);
            _entityId = new Lazy<string?>(() => entityId);
        }

        /// <summary>
        /// Gets or sets the container tags hash received from the agent, used by DBM/DSM
        /// This is set when we receive a value for it in an http response from the agent
        /// </summary>
        public string? ContainerTagsHash
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        }

        /// <summary>
        /// Gets the id of the container executing the code.
        /// Return <c>null</c> if code is not executing inside a supported container.
        /// </summary>
        /// <value>The container id or <c>null</c>.</value>
        public string? ContainerId
        {
            get => _containerId.Value;
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
        /// <value>The entity id or <c>null</c>.</value>
        public string? EntityId
        {
            get => _entityId.Value;
        }

        /// <summary>
        /// Uses regular expression to try to extract a container id from the specified string.
        /// </summary>
        /// <param name="lines">Lines of text from a cgroup file.</param>
        /// <returns>The container id if found; otherwise, <c>null</c>.</returns>
        public static string? ParseContainerIdFromCgroupLines(IEnumerable<string> lines)
        {
            return lines.Select(ParseContainerIdFromCgroupLine)
                        .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        /// <summary>
        /// Uses regular expression to try to extract a container id from the specified string.
        /// </summary>
        /// <param name="line">A single line from a cgroup file.</param>
        /// <returns>The container id if found; otherwise, <c>null</c>.</returns>
        public static string? ParseContainerIdFromCgroupLine(string line)
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
        public static string? ExtractInodeFromCgroupLines(string controlGroupsMountPath, IEnumerable<string> lines)
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
        public static Tuple<string, string>? ParseControllerAndPathFromCgroupLine(string line)
        {
            var lineMatch = Regex.Match(line, CgroupRegex);

            return lineMatch.Success
                       ? new(lineMatch.Groups[1].Value, lineMatch.Groups[2].Value)
                       : null;
        }

        internal static bool TryGetInode(string path, out long result)
            => TryGetInodeUsingPInvoke(path, out result)
            || TryGetInodeUsingStat(path, out result);

        [TestingAndPrivateOnly]
        internal static bool TryGetInodeUsingPInvoke(string path, out long result)
        {
            result = 0;

            try
            {
                if (!NativeMethods.TryGetInodeForPath(path, out result))
                {
                    LogError(null, "Error obtaining inode using PInvoke, returned " + result);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "Error obtaining inode using PInvoke");
                return false;
            }

            static void LogError(Exception? ex, string message)
            {
#pragma warning disable DDLOG004 // Must use constant strings - disabled as it's an integer only, and only called twice in the app lifetime
                if (EnvironmentHelpersNoLogging.IsClrProfilerAttachedSafe())
                {
                    // if it's attached, then this really is an error
                    Log.Error(ex, message);
                }
                else
                {
                    // The profiler isn't there, so this is "normal"
                    Log.Debug(ex, message);
                }
            }
#pragma warning restore DDLOG004
        }

        [TestingAndPrivateOnly]
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

        private static string? GetContainerIdInternal()
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

        private static string? GetEntityIdInternal(Lazy<string?> lazyContainerId)
        {
            if (lazyContainerId.Value is string containerId)
            {
                return $"ci-{containerId}";
            }
            else if (GetCgroupInode() is string cgroupInode)
            {
                return $"in-{cgroupInode}";
            }
            else
            {
                return null;
            }
        }

        private static string? GetCgroupInode()
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
    }
}
#endif
