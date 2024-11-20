using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GeneratePackageVersions
{
    internal class GenerateIntegrationMarkdownTable
    {
        private class TestVersionInfo
        {
            public string SampleName { get; set; }
            public string[] IntegrationNames { get; set; }
            public Version Version { get; set; }
        }

        private class Package
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("minVersionTestedInclusive")]
            public string MinVersionTested { get; set; }

            [JsonPropertyName("maxVersionTestedInclusive")]
            public string MaxVersionTested { get; set; }
        }

        private class Integration
        {
            [JsonPropertyName("integrationName")]
            public string IntegrationName { get; set; }

            [JsonPropertyName("assemblyName")]
            public string AssemblyName { get; set; }

            [JsonPropertyName("minAssemblyVersionInclusive")]
            public string MinAssemblyVersion { get; set; }

            [JsonPropertyName("maxAssemblyVersionInclusive")]
            public string MaxAssemblyVersion { get; set; }

            [JsonPropertyName("packages")]
            public List<Package> Packages { get; set; }
        }

        public static void GenerateEnhancedTable(
           string supportedVersionsPath,
           string propsDirectory,
           string sourcePath,
           string output)
        {
            // Get supported versions from JSON
            var supportedVersionsJson = File.ReadAllText(supportedVersionsPath);
            var integrations = JsonSerializer.Deserialize<List<Integration>>(supportedVersionsJson);

            // Extract tested versions from props files
            var testedVersions = ExtractTestedVersionsFromProps(propsDirectory);

            using var writer = new StreamWriter(output);
            writer.WriteLine("| Integration | Type | Name | Minimum Version | Maximum Version |");
            writer.WriteLine("|------------|------|------|----------------|----------------|");

            foreach (var group in integrations.GroupBy(i => i.IntegrationName).OrderBy(g => g.Key))
            {
                var isFirst = true;
                foreach (var integration in group)
                {
                    // Write assembly info - for assemblies, we use the supported min/max as the tested min/max
                    var integrationName = isFirst ? integration.IntegrationName : "";

                    writer.WriteLine(
                        $"| {integrationName} | Assembly | {integration.AssemblyName} | {integration.MinAssemblyVersion} | {integration.MaxAssemblyVersion} |");

                    // Write package info if any
                    if (integration.Packages?.Any() == true)
                    {
                        foreach (var package in integration.Packages)
                        {
                            // Try to get tested versions from props files, otherwise fall back to supported versions
                            var (minVersion, maxVersion) = testedVersions.TryGetValue((integration.IntegrationName, package.Name), out var tested)
                                ? (tested.MinVersion.ToString(), tested.MaxVersion.ToString())
                                : (package.MinVersionTested ?? "-", package.MaxVersionTested ?? "-");

                            writer.WriteLine(
                                $"| | Package | {package.Name} | {minVersion} | {maxVersion} |");
                        }
                    }

                    isFirst = false;
                }
            }
        }

        private static Dictionary<(string Integration, string Package), (Version MinVersion, Version MaxVersion)> ExtractTestedVersionsFromProps(string propsDirectory)
        {
            var result = new Dictionary<(string Integration, string Package), (Version MinVersion, Version MaxVersion)>();

            // Find all .g.props files
            var propsFiles = Directory.GetFiles(propsDirectory, "PackageVersions*.g.props");

            foreach (var filePath in propsFiles)
            {
                var doc = XDocument.Load(filePath);
                var samples = doc.Descendants("PackageVersionSample");

                foreach (var sample in samples)
                {
                    var properties = sample.Element("Properties")?.Value ?? "";
                    var versionMatch = Regex.Match(properties, @"ApiVersion=([^;]+)");
                    if (!versionMatch.Success || !Version.TryParse(versionMatch.Groups[1].Value, out var version))
                    {
                        continue;
                    }

                    // Get integration names - handle both formats
                    var integrationNames = sample.Element("IntegrationNames")?.Value?.Split(';')
                                       ?? new[] { sample.Element("IntegrationName")?.Value };

                    if (integrationNames.All(string.IsNullOrEmpty))
                    {
                        continue;
                    }

                    // Extract the sample project name from the path
                    var samplePath = sample.Attribute("Include")?.Value;
                    var sampleMatch = Regex.Match(samplePath ?? "", @"integrations\\([^\\]+)\\");
                    if (!sampleMatch.Success) continue;
                    var sampleName = sampleMatch.Groups[1].Value;

                    // Get NuGet package name - either from Properties or fallback to sample name
                    var packageName = GetPackageName(properties, sampleName);

                    foreach (var integrationName in integrationNames.Where(x => !string.IsNullOrEmpty(x)))
                    {
                        var key = (integrationName.Trim(), packageName);
                        if (result.TryGetValue(key, out var existing))
                        {
                            result[key] = (
                                MinVersion: version < existing.MinVersion ? version : existing.MinVersion,
                                MaxVersion: version > existing.MaxVersion ? version : existing.MaxVersion);
                        }
                        else
                        {
                            result[key] = (version, version);
                        }
                    }
                }
            }

            return result;
        }

        private static string GetPackageName(string properties, string defaultName)
        {
            // Custom logic to handle specific package mappings that differ from the sample name
            var nugetPackageMatch = Regex.Match(properties, @"NuGetPackageName=([^;]+)");
            if (nugetPackageMatch.Success)
            {
                return nugetPackageMatch.Groups[1].Value;
            }

            // Special cases where package name differs from sample name
            return defaultName switch
            {
                "Samples.MongoDB" => "MongoDB.Driver",
                "Samples.MySql" => "MySql.Data",
                // Add other special cases as needed
                _ => defaultName
            };
        }

        public static void GenerateTableFromProps(string propsDirectory, string outputPath)
        {
            var files = new[]
            {
                "PackageVersionsLatestMajors.g.props",
                "PackageVersionsLatestMinors.g.props",
                "PackageVersionsLatestSpecific.g.props"
            };

            var allVersions = new List<TestVersionInfo>();

            foreach (var file in files)
            {
                var filePath = Path.Combine(propsDirectory, file);
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Warning: Props file not found: {filePath}");
                    continue;
                }

                allVersions.AddRange(ExtractVersionsFromPropsFile(filePath));
            }

            // Group by integration name and calculate version ranges
            var integrationRanges = allVersions
                .SelectMany(v => v.IntegrationNames.Select(i => new { IntegrationName = i, v.SampleName, v.Version }))
                .GroupBy(x => (x.IntegrationName, x.SampleName))
                .Select(g => new
                {
                    IntegrationName = g.Key.IntegrationName,
                    SampleName = g.Key.SampleName,
                    MinVersion = g.Min(x => x.Version),
                    MaxVersion = g.Max(x => x.Version)
                })
                .GroupBy(x => x.IntegrationName)
                .OrderBy(g => g.Key);

            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("| Integration | Sample Project | Minimum Version Tested | Maximum Version Tested |");
            writer.WriteLine("|------------|----------------|----------------------|----------------------|");

            foreach (var integration in integrationRanges)
            {
                var isFirst = true;
                foreach (var sample in integration.OrderBy(x => x.SampleName))
                {
                    writer.WriteLine(
                        $"| {(isFirst ? integration.Key : "")} | {sample.SampleName} | {sample.MinVersion} | {sample.MaxVersion} |");
                    isFirst = false;
                }
            }
        }

        private static IEnumerable<TestVersionInfo> ExtractVersionsFromPropsFile(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var samples = doc.Descendants("PackageVersionSample");
            var versions = new List<TestVersionInfo>();

            foreach (var sample in samples)
            {
                var sampleName = sample.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(sampleName))
                {
                    continue;
                }

                // Extract the sample name from the path
                var match = Regex.Match(sampleName, @"integrations\\([^\\]+)\\");
                if (!match.Success)
                {
                    continue;
                }
                sampleName = match.Groups[1].Value;

                // Get integration names
                var integrationNames = sample.Element("IntegrationNames")?.Value.Split(';') ??
                                     new[] { sample.Element("IntegrationName")?.Value }; // fallback for older format

                if (integrationNames.All(x => string.IsNullOrEmpty(x)))
                {
                    continue;
                }

                // Extract API version from Properties
                var properties = sample.Element("Properties")?.Value;
                if (string.IsNullOrEmpty(properties))
                {
                    continue;
                }

                var versionMatch = Regex.Match(properties, @"ApiVersion=([^;]+)");
                if (!versionMatch.Success)
                {
                    continue;
                }

                if (Version.TryParse(versionMatch.Groups[1].Value, out var version))
                {
                    versions.Add(new TestVersionInfo
                    {
                        SampleName = sampleName,
                        IntegrationNames = integrationNames.Where(x => !string.IsNullOrEmpty(x)).ToArray(),
                        Version = version
                    });
                }
            }

            return versions;
        }



        public static void GenerateTable(string input, string output)
        {
            var jsonContent = File.ReadAllText(input);
            var integrations = JsonSerializer.Deserialize<List<Integration>>(jsonContent);

            var distinctIntegrations = integrations
                .GroupBy(i => i.IntegrationName)
                .Select(group =>
                {
                    var assemblies = group.Select(i => new
                    {
                        Name = i.AssemblyName,
                        MinVersion = i.MinAssemblyVersion,
                        MaxVersion = i.MaxAssemblyVersion,
                        Packages = i.Packages ?? new List<Package>()
                    }).ToList();

                    return new
                    {
                        IntegrationName = group.Key,
                        Implementations = assemblies.Select(assembly => new
                        {
                            Type = "Assembly",
                            Name = assembly.Name,
                            MinVersion = assembly.MinVersion,
                            MaxVersion = assembly.MaxVersion
                        })
                        .Concat(
                            assemblies.SelectMany(a => a.Packages)
                                .Where(p => p.MinVersionTested != null || p.MaxVersionTested != null)
                                .Select(p => new
                                {
                                    Type = "Package",
                                    Name = p.Name,
                                    MinVersion = p.MinVersionTested ?? "-",
                                    MaxVersion = p.MaxVersionTested ?? "-"
                                })
                        )
                        .OrderBy(x => x.Type)
                        .ThenBy(x => x.Name)
                        .ToList()
                    };
                })
                .OrderBy(i => i.IntegrationName);

            using var writer = new StreamWriter(output);
            writer.WriteLine("| Integration | Type | Name | Minimum Version | Maximum Version |");
            writer.WriteLine("|------------|------|------|----------------|----------------|");

            foreach (var integration in distinctIntegrations)
            {
                if (!integration.Implementations.Any())
                {
                    // Write a single row for integrations with no implementations
                    writer.WriteLine($"| {integration.IntegrationName} | - | - | - | - |");
                }
                else
                {
                    // Write a row for each implementation (both assemblies and packages)
                    var isFirst = true;
                    foreach (var impl in integration.Implementations)
                    {
                        var integrationName = isFirst ? integration.IntegrationName : "";
                        writer.WriteLine($"| {integrationName} | {impl.Type} | {impl.Name} | {impl.MinVersion} | {impl.MaxVersion} |");
                        isFirst = false;
                    }
                }
            }
        }
    }
}
