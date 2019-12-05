using System.IO;
using Datadog.Core.Tools;
using PrepareRelease.Tools;

namespace PrepareRelease
{
    public static class SyncAzureAppServicesPackage
    {
        private static readonly string TracerHomeDirectory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "deploy", "AzureAppServices", "content", "Tracer");

        public static void Run()
        {
            CopyTargetFrameworkPrivateBin("net45");
            CopyTargetFrameworkPrivateBin("net461");
            CopyTargetFrameworkPrivateBin("netstandard2.0");
            CopyNativeProfiler("x86");
            CopyNativeProfiler("x64");
            CopyIntegrationsJson();
        }

        private static void CopyTargetFrameworkPrivateBin(string frameworkMoniker)
        {
            var outputDirectory = Path.Combine(TracerHomeDirectory, frameworkMoniker);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }

            Directory.CreateDirectory(outputDirectory);

            var sourceFiles = DependencyHelpers.GetTracerReleaseBinContent(frameworkMoniker);

            foreach (var sourceFile in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var destinationPath = Path.Combine(outputDirectory, fileName);
                File.Copy(sourceFile, destinationPath);
            }
        }

        private static void CopyNativeProfiler(string architecture)
        {
            var outputDirectory = Path.Combine(TracerHomeDirectory, architecture);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }

            Directory.CreateDirectory(outputDirectory);

            var sourceFiles = DependencyHelpers.GetProfilerReleaseBinContent(architecture);

            foreach (var sourceFile in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var destinationPath = Path.Combine(outputDirectory, fileName);
                File.Copy(sourceFile, destinationPath);
            }
        }

        private static void CopyIntegrationsJson()
        {
            var sourceFile = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "integrations.json");
            var destinationPath = Path.Combine(TracerHomeDirectory, "integrations.json");

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Copy(sourceFile, destinationPath);
        }
    }
}
