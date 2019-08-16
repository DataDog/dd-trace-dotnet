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
                return ParseLines(lines);
            }

            return null;
        }

        internal static string ParseFile(string file)
        {
            if (file == null)
            {
                return null;
            }

            IEnumerable<string> lines = SplitLines(file);
            return ParseLines(lines);
        }

        internal static IEnumerable<string> SplitLines(string lines)
        {
            if (lines == null)
            {
                yield break;
            }

            using (var reader = new StringReader(lines))
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

        internal static string ParseLines(IEnumerable<string> lines)
        {
            return lines.Select(ParseLine)
                        .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        internal static string ParseLine(string line)
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
