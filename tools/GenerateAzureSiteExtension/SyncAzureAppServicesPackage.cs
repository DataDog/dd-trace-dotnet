using System;
using System.IO;
using Datadog.Core.Tools;

namespace GenerateAzureSiteExtension
{
    public static class SyncAzureAppServicesPackage
    {
        private static string _tracerHomeDirectory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "deploy", "AzureAppServices");

        public static void Run()
        {
            var pathVariableKey = "DD_AAS_PACKAGING_PATH";
            var packagingPath = Environment.GetEnvironmentVariable(pathVariableKey);
            if (string.IsNullOrWhiteSpace(packagingPath))
            {
                Console.WriteLine($"WARNING: You are using the default output path of: {_tracerHomeDirectory}");
                Console.WriteLine($"WARNING: If you wish to sent to a specific path, set the {pathVariableKey} environment variable.");
            }
            else
            {
                _tracerHomeDirectory = packagingPath;
            }

            CopyTargetFrameworkPrivateBin("net45");
            CopyTargetFrameworkPrivateBin("net461");
            CopyTargetFrameworkPrivateBin("netstandard2.0");
            CopyNativeProfiler("x86");
            CopyNativeProfiler("x64");
            CopyIntegrationsJson();
        }

        private static void CopyTargetFrameworkPrivateBin(string frameworkMoniker)
        {
            var outputDirectory = Path.Combine(_tracerHomeDirectory, frameworkMoniker);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }

            Directory.CreateDirectory(outputDirectory);

            var sourceFiles = DependencyHelpers.GetTracerBinContent(frameworkMoniker);

            foreach (var sourceFile in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var destinationPath = Path.Combine(outputDirectory, fileName);
                File.Copy(sourceFile, destinationPath);
            }
        }

        private static void CopyNativeProfiler(string architecture)
        {
            var outputDirectory = Path.Combine(_tracerHomeDirectory, architecture);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }

            Directory.CreateDirectory(outputDirectory);

            var sourceFiles = DependencyHelpers.GetTracerBinContent(architecture);

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
            var destinationPath = Path.Combine(_tracerHomeDirectory, "integrations.json");

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Copy(sourceFile, destinationPath);
        }
    }
}
