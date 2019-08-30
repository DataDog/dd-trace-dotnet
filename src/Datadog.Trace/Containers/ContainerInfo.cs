using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Containers
{
    /// <summary>
    /// Utility class with methods to interact with container hosts.
    /// </summary>
    internal static class ContainerInfo
    {
        private const string ControlGroupsFilePath = "/proc/self/cgroup";

        private const string ContainerIdRegex = @"^(?:\d+):(?:[^:]*):/?(?:.+/)([0-9a-f]{8}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{12}|[0-9a-f]{64}(?:\.scope)?)$";

        private static readonly Lazy<string> ContainerId = new Lazy<string>(GetContainerIdInternal, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

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
        /// Uses regular expression to try to extract a container id from the specified string.
        /// </summary>
        /// <param name="lines">Lines of text from a cgroup file.</param>
        /// <returns>The container id if found; otherwise, <c>null</c>.</returns>
        public static string ParseCgroupLines(IEnumerable<string> lines)
        {
            return lines.Select(ParseCgroupLine)
                        .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        /// <summary>
        /// Uses regular expression to try to extract a container id from the specified string.
        /// </summary>
        /// <param name="line">A single line from a cgroup file.</param>
        /// <returns>The container id if found; otherwise, <c>null</c>.</returns>
        public static string ParseCgroupLine(string line)
        {
            var lineMatch = Regex.Match(line, ContainerIdRegex);

            return lineMatch.Success
                       ? lineMatch.Groups[1].Value
                       : null;
        }

        private static string GetContainerIdInternal()
        {
            bool isLinux;

            try
            {
                isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            }
            catch (Exception ex)
            {
                Log.WarnException("Unable to determine OS. Will not report container id.", ex);
                return null;
            }

            try
            {
                if (isLinux && File.Exists(ControlGroupsFilePath))
                {
                    var lines = File.ReadLines(ControlGroupsFilePath);
                    return ParseCgroupLines(lines);
                }
            }
            catch (Exception ex)
            {
                Log.WarnException("Error reading cgroup file. Will not report container id.", ex);
                return null;
            }

            return null;
        }
    }
}
