using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Datadog.Trace.Containers
{
    /// <summary>
    /// Utility class with methods to interact with container hosts.
    /// </summary>
    internal static class ContainerInfo
    {
        private const string ControlGroupsFilePath = "/proc/self/cgroup";

        private const string LineRegex = @"^(?:\d+):(?:[^:]*):(.+)$";

        private const string ContainerIdRegex = @"(?:pod)?" +
                                                @"([0-9a-f]{8}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{12}|[0-9a-f]{64})" +
                                                @"(?:\.scope|\.slice)?$";

        /// <summary>
        /// Gets the id of the container executing the code.
        /// Return <c>null</c> if code is not executing inside a supported container.
        /// </summary>
        /// <returns>The container id or <c>null</c>.</returns>
        public static string GetContainerId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists(ControlGroupsFilePath))
            {
                IEnumerable<string> lines = File.ReadLines(ControlGroupsFilePath);
                return ParseCgroupLines(lines);
            }

            return null;
        }

        /// <summary>
        /// Extract the container id from the specified cgroup file contents.
        /// </summary>
        /// <remarks>
        /// This method is used when passing the entire cgroup text for testing purposes.
        /// When reading from file system, we only read one line at a time until we find a match,
        /// which is usually the first line.
        /// </remarks>
        /// <param name="contents">The contents of a cgroup file.</param>
        /// <returns>The container id if a match is found; otherwise, <c>null</c>.</returns>
        public static string ParseCgroupText(string contents)
        {
            if (contents == null)
            {
                return null;
            }

            IEnumerable<string> lines = SplitLines(contents);
            return ParseCgroupLines(lines);
        }

        /// <summary>
        /// Used by <see cref="ParseCgroupText"/> to split the entire file contents
        /// into individual lines to mock <see cref="File.ReadLines(string)"/>.
        /// </summary>
        /// <param name="contents">The multi-line string.</param>
        /// <returns>An enumerable that returns each line from <paramref name="contents"/> when iterated.</returns>
        public static IEnumerable<string> SplitLines(string contents)
        {
            if (contents == null)
            {
                yield break;
            }

            using (var reader = new StringReader(contents))
            {
                while (true)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                    {
                        yield break;
                    }

                    yield return line;
                }
            }
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
            var lineMatch = Regex.Match(line, LineRegex);

            if (lineMatch.Success)
            {
                string path = lineMatch.Groups[1].Value;
                string lastPathPart = path.Split('/').Last();

                var containerIdMatch = Regex.Match(lastPathPart, ContainerIdRegex);

                if (containerIdMatch.Success)
                {
                    return containerIdMatch.Groups[1].Value;
                }
            }

            return null;
        }
    }
}
