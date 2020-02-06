using System;
using System.IO;

namespace Datadog.Core.Tools
{
    public static class DependencyHelpers
    {
        public static string[] GetTracerBinContent(string frameworkMoniker, string extension = "dll")
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
            var projectBin =
                Path.Combine(
                    solutionDirectory,
                    "tools",
                    "PrepareRelease",
                    "bin",
                    "tracer-home");

            var outputFolder = Path.Combine(projectBin, frameworkMoniker);

            var filePaths = Directory.GetFiles(
                outputFolder,
                $"*.{extension}",
                SearchOption.AllDirectories);

            if (filePaths.Length == 0)
            {
                throw new Exception("Be sure to build in release mode before running this tool.");
            }

            return filePaths;
        }
    }
}
