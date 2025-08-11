using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Nuke.Common.IO;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

namespace CodeGenerators
{
    /// <summary>
    /// Generates CSV files containing information about supported integrations from the supported_versions.json file.
    /// </summary>
    public static class SupportedIntegrationsTableGenerator
    {
        public class SupportedVersion
        {
            public string IntegrationName { get; set; }
            public string AssemblyName { get; set; }
            public string MinAssemblyVersionInclusive { get; set; }
            public string MaxAssemblyVersionInclusive { get; set; }
            public List<Package> Packages { get; set; }
        }

        public class Package
        {
            public string Name { get; set; }
            public string MinVersionAvailableInclusive { get; set; }
            public string MinVersionSupportedInclusive { get; set; }
            public string MinVersionTestedInclusive { get; set; }
            public string MaxVersionSupportedInclusive { get; set; }
            public string MaxVersionAvailableInclusive { get; set; }
            public string MaxVersionTestedInclusive { get; set; }
        }

        public class IntegrationInfo
        {
            public string IntegrationName { get; set; }
            public string PackageName { get; set; }
            public string MinVersion { get; set; }
            public string MaxVersion { get; set; }
            public bool IsNetCore { get; set; }
            public bool IsNetFramework { get; set; }
        }

        // skip these integrations as they are helpers/utilities, not real integrations
        // Well OpenTelemetry is a real integration, but unsure how to present that
        private static readonly HashSet<string> HelperIntegrations = new()
        {
            "CallTargetNativeTest",
            "ServiceRemoting",
            "OpenTelemetry",
            "AssemblyResolve"
        };

        // not 100% sure on these TBH would have to check
        private static readonly HashSet<string> NetCoreOnlyIntegrations = new()
        {
            "AspNetCore",
            "Grpc",
            "HotChocolate"
        };

        private static readonly HashSet<string> NetFrameworkOnlyIntegrations = new()
        {
            "AspNet",
            "AspNetMvc",
            "AspNetWebApi2",
            "Msmq",
            "Owin",
            "Remoting",
            "Wcf"
        };

        /// <summary>
        /// Generates CSV files containing supported integrations information.
        /// </summary>
        /// <param name="supportedVersionsPath">Path to the supported_versions.json file</param>
        /// <param name="outputDirectory">Directory where CSV files will be saved</param>
        public static void GenerateCsvFiles(AbsolutePath supportedVersionsPath, AbsolutePath outputDirectory)
        {
            Logger.Information("Reading supported versions from {Path}", supportedVersionsPath);

            var supportedVersions = ReadSupportedVersions(supportedVersionsPath);
            var integrations = ProcessIntegrations(supportedVersions);

            GenerateOutputFiles(integrations, outputDirectory);
        }

        private static List<SupportedVersion> ReadSupportedVersions(AbsolutePath supportedVersionsPath)
        {
            var json = File.ReadAllText(supportedVersionsPath);
            return JsonSerializer.Deserialize<List<SupportedVersion>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private static void GenerateOutputFiles(List<IntegrationInfo> integrations, AbsolutePath outputDirectory)
        {
            // Generate .NET Core/.NET CSV
            GenerateCsv(
                integrations.Where(i => i.IsNetCore).OrderBy(i => i.IntegrationName).ThenBy(i => i.PackageName),
                outputDirectory / "supported_integrations_netcore.csv",
                ".NET Core / .NET");

            // Generate .NET Framework CSV
            GenerateCsv(
                integrations.Where(i => i.IsNetFramework).OrderBy(i => i.IntegrationName).ThenBy(i => i.PackageName),
                outputDirectory / "supported_integrations_netfx.csv",
                ".NET Framework");

            // Generate combined CSV
            GenerateCsv(
                integrations.OrderBy(i => i.IntegrationName).ThenBy(i => i.PackageName),
                outputDirectory / "supported_integrations.csv",
                "All Frameworks");
        }

        private static List<IntegrationInfo> ProcessIntegrations(List<SupportedVersion> supportedVersions)
        {
            var integrations = new List<IntegrationInfo>();

            foreach (var version in supportedVersions)
            {
                if (IsHelperIntegration(version.IntegrationName))
                    continue;

                if (version.Packages != null && version.Packages.Any())
                {
                    foreach (var package in version.Packages)
                    {
                        var info = CreateIntegrationInfo(version, package);
                        integrations.Add(info);
                    }
                }
                else
                {
                    var info = CreateIntegrationInfoFromAssembly(version);
                    integrations.Add(info);
                }
            }

            return integrations;
        }

        private static IntegrationInfo CreateIntegrationInfo(SupportedVersion version, Package package)
        {
            // For packages with NuGet packages, use minVersionTestedInclusive as min
            // and the floating major version of maxVersionTestedInclusive as max
            var minVersion = package.MinVersionTestedInclusive ?? package.MinVersionSupportedInclusive ?? version.MinAssemblyVersionInclusive;
            var maxVersion = package.MaxVersionTestedInclusive ?? package.MaxVersionSupportedInclusive ?? version.MaxAssemblyVersionInclusive;

            return new IntegrationInfo
            {
                IntegrationName = version.IntegrationName,
                PackageName = package.Name,
                MinVersion = minVersion,
                MaxVersion = GetFloatingMajorVersion(maxVersion),  // Use floating version for max
                IsNetCore = !NetFrameworkOnlyIntegrations.Contains(version.IntegrationName),
                IsNetFramework = !NetCoreOnlyIntegrations.Contains(version.IntegrationName)
            };
        }

        private static IntegrationInfo CreateIntegrationInfoFromAssembly(SupportedVersion version)
        {
            // For assemblies without NuGet packages, use minAssemblyVersionInclusive as min
            // and the floating version of maxAssemblyVersionInclusive as max
            return new IntegrationInfo
            {
                IntegrationName = version.IntegrationName,
                PackageName = version.AssemblyName,  // Use assembly name as package name
                MinVersion = version.MinAssemblyVersionInclusive,
                MaxVersion = GetFloatingMajorVersion(version.MaxAssemblyVersionInclusive),  // Use floating version for max
                IsNetCore = !NetFrameworkOnlyIntegrations.Contains(version.IntegrationName),
                IsNetFramework = !NetCoreOnlyIntegrations.Contains(version.IntegrationName)
            };
        }

        private static bool IsHelperIntegration(string integrationName)
        {
            return HelperIntegrations.Contains(integrationName);
        }

        private static string GetFloatingMajorVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "N/A";

            // Parse the version string to get the major version
            var parts = version.Split('.');
            if (parts.Length > 0)
            {
                // Return major version with .x suffix
                return parts[0] + ".x";
            }

            return version;
        }

        private static void GenerateCsv(IEnumerable<IntegrationInfo> integrations, AbsolutePath outputPath, string framework)
        {
            var sb = new StringBuilder();
            // Simple 4-column CSV format
            sb.AppendLine("integration,package,min_version,max_version");

            foreach (var integration in integrations)
            {
                sb.AppendLine($"{integration.IntegrationName},{integration.PackageName},{integration.MinVersion},{integration.MaxVersion}");
            }

            EnsureExistingDirectory(outputPath.Parent);
            File.WriteAllText(outputPath, sb.ToString());

            Logger.Information("Generated {Framework} supported integrations CSV: {Path}", framework, outputPath);
            Logger.Information("Total integrations: {Count}", integrations.Count());
        }
    }
}
