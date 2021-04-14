using System;
using System.IO;
using System.Linq;

namespace Datadog.Core.Tools
{
    public static class DependencyHelpers
    {
        public static string[] GetTracerBinContent(string frameworkMoniker, string[] extensions)
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
            var projectBin =
                Path.Combine(
                    solutionDirectory,
                    "build",
                    "tools",
                    "PrepareRelease",
                    "bin",
                    "tracer-home");

            var outputFolder = Path.Combine(projectBin, frameworkMoniker);

            var filePaths = Directory.EnumerateFiles(
                                          outputFolder,
                                          "*.*",
                                          SearchOption.AllDirectories)
                                     .Where(f => extensions.Contains(Path.GetExtension(f)))
                                     .ToArray();

            if (filePaths.Length == 0)
            {
                throw new Exception("Be sure to build in release mode before running this tool.");
            }

            return filePaths;
        }
    }
}
