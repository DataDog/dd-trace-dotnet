using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Honeypot;

namespace GeneratePackageVersions;

#nullable enable

/// <summary>
/// Used to generate the matrix of dependencies that we support
/// </summary>
public static class GenerateSupportMatrix
{
    public static async Task GenerateInstrumentationSupportMatrix(
        string outputPath,
        List<IntegrationMap> instrumentedAssemblies)
    {
        List<Integration> integrations = new(instrumentedAssemblies.Count);
        
        foreach (var instrumentedAssembly in instrumentedAssemblies)
        {
            var integration = new Integration
            {
                IntegrationName = instrumentedAssembly.IntegrationId,
                AssemblyName = instrumentedAssembly.AssemblyName,
                MinAssemblyVersionInclusive = instrumentedAssembly.MinimumSupportedAssemblyVersion.ToString(),
                MaxAssemblyVersionInclusive = instrumentedAssembly.MaximumSupportedAssemblyVersion.ToString(),
            };

            foreach (var package in instrumentedAssembly.Packages.OrderBy(x=>x.NugetName))
            {
                integration.Packages.Add(new()
                {
                    Name = package.NugetName,
                    MinVersionAvailableInclusive = package.FirstVersion.ToString(),
                    MaxVersionAvailableInclusive = package.LatestVersion.ToString(),
                    MinVersionSupportedInclusive = package.FirstSupportedVersion.ToString(),
                    MaxVersionSupportedInclusive = package.LatestSupportedVersion.ToString(),
                    MinVersionTestedInclusive = package.FirstTestedVersion?.ToString(),
                    MaxVersionTestedInclusive = package.LatestTestedVersion?.ToString(),
                });
            }
            
            integrations.Add(integration);
        }

        var toWrite = integrations
            .OrderBy(x => x.IntegrationName)
            .ThenBy(x => x.AssemblyName)
            .ThenBy(x => x.MinAssemblyVersionInclusive);

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        await using var file = File.Open(outputPath, FileMode.Create);
        await JsonSerializer.SerializeAsync(file, toWrite, jsonSerializerOptions );
    }

    /// <summary>
    /// Load the previously generated supported_versions.json and return a lookup
    /// keyed by NuGet package name, mapping to its <see cref="SupportedNuGetPackage"/> data.
    /// Returns an empty dictionary if the file doesn't exist.
    /// </summary>
    public static async Task<Dictionary<string, SupportedNuGetPackage>> LoadPreviousVersions(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, SupportedNuGetPackage>(StringComparer.OrdinalIgnoreCase);
        }

        var JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        await using var openStream = File.OpenRead(path);
        var integrations = await JsonSerializer.DeserializeAsync<List<Integration>>(openStream, JsonOptions);

        var result = new Dictionary<string, SupportedNuGetPackage>(StringComparer.OrdinalIgnoreCase);
        if (integrations is null)
        {
            return result;
        }

        foreach (var integration in integrations)
        {
            foreach (var package in integration.Packages)
            {
                // Last write wins if a package appears under multiple assemblies
                result[package.Name] = package;
            }
        }

        return result;
    }

    public class Integration
    {
        /// <summary>
        /// The IntegrationId name
        /// </summary>
        public required string IntegrationName { get; init; }
        
        /// <summary>
        /// The actual assembly we instrument
        /// </summary>
        public required string AssemblyName { get; init; }
        
        /// <summary>
        /// The min version of the assembly that we instrument. This is generally fixed
        /// </summary>
        public required string MinAssemblyVersionInclusive { get; init; }
        
        /// <summary>
        /// The max version of the assembly that we instrument. This will change as new versions
        /// of packages are updated. 
        /// </summary>
        public required string MaxAssemblyVersionInclusive { get; init; }

        /// <summary>
        /// If applicable, the name of the NuGet package in which we expect to find the assemblies.
        /// If empty, the package is not available in NuGet packages (e.g. installed as part of the framework)
        /// </summary>
        public List<SupportedNuGetPackage> Packages { get; set; } = new();
    }

    public class SupportedNuGetPackage
    {
        /// <summary>
        /// If applicable, the name of the NuGet package in which we expect to find the assemblies
        /// that we instrument 
        /// </summary>
        public required string Name { get; init; }
        
        /// <summary>
        /// The minimum version of the NuGet package that is available
        /// </summary>
        public required string MinVersionAvailableInclusive { get; init; }

        /// <summary>
        /// The minimum version of the NuGet package that we instrument
        /// </summary>
        public required string MinVersionSupportedInclusive { get; init; }

        /// <summary>
        /// The minimum version of the NuGet package that we test in CI
        /// </summary>
        public required string? MinVersionTestedInclusive { get; init; }
        
        /// <summary>
        /// The maximum version of the NuGet package that is available
        /// </summary>
        public required string MaxVersionSupportedInclusive { get; init; }

        /// <summary>
        /// The maximum version of the NuGet package that we instrument
        /// </summary>
        public required string MaxVersionAvailableInclusive { get; init; }
        
        /// <summary>
        /// The maximum version of the NuGet package that we test in CI
        /// </summary>
        public required string? MaxVersionTestedInclusive { get; init; }
    }
}
