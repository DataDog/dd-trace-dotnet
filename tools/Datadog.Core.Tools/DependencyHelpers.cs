using System;
using System.IO;
using Datadog.Core.Tools;

namespace PrepareRelease.Tools
{
    public static class DependencyHelpers
    {
        private const string RequiredBuildConfig = "Release";

        public static string[] GetTracerReleaseBinContent(string frameworkMoniker, string extension = "dll")
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

        public static string[] GetProfilerReleaseBinContent(string architecture, string extension = "dll")
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
            var outputFolder =
                Path.Combine(
                    solutionDirectory,
                    "src",
                    "Datadog.Trace.ClrProfiler.Native",
                    "bin",
                    RequiredBuildConfig,
                    architecture);

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
