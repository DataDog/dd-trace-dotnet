// <copyright file="TestManifestGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GeneratePackageVersions;

/// <summary>
/// Generates the package_versions.json manifest consumed by tests at runtime.
/// This replaces the old generated .g.cs files that used #if preprocessor directives.
/// Tests load this JSON at runtime and filter by the current target framework.
/// </summary>
public static class TestManifestGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Generates a manifest file from the resolved versions.
    /// </summary>
    /// <param name="outputPath">Path to write the JSON file.</param>
    /// <param name="result">The generation result.</param>
    /// <param name="selector">Selects which strategy (LatestMinors or LatestSpecific) to emit.</param>
    public static async Task Generate(
        string outputPath,
        GenerationResult result,
        Func<IntegrationVersions, List<FrameworkVersionGroup>> selector)
    {
        // Load existing manifest so we can preserve unchanged integrations
        var existingIntegrations = new Dictionary<string, IntegrationManifestEntry>();
        if (File.Exists(outputPath))
        {
            var existingJson = await File.ReadAllTextAsync(outputPath);
            var existing = JsonSerializer.Deserialize<PackageVersionManifest>(existingJson, JsonOptions);
            if (existing?.Integrations is not null)
            {
                existingIntegrations = existing.Integrations;
            }
        }

        var manifest = new PackageVersionManifest
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Integrations = new Dictionary<string, IntegrationManifestEntry>(),
        };

        foreach (var integration in result.Integrations)
        {
            var definition = integration.Definition;

            if (integration.Unchanged)
            {
                // Preserve existing entry for this integration
                if (existingIntegrations.TryGetValue(definition.IntegrationName, out var existingEntry))
                {
                    manifest.Integrations[definition.IntegrationName] = existingEntry;
                }

                continue;
            }

            var groups = selector(integration);

            // Pivot from (framework -> versions) to (version -> frameworks)
            var versionToFrameworks = new Dictionary<string, List<string>>();
            var versionSkipAlpine = new Dictionary<string, bool>();
            var versionSkipArm64 = new Dictionary<string, bool>();

            foreach (var group in groups)
            {
                foreach (var version in group.Versions)
                {
                    var key = version.ToString();
                    if (!versionToFrameworks.ContainsKey(key))
                    {
                        versionToFrameworks[key] = new List<string>();
                        versionSkipAlpine[key] = definition.ShouldSkipAlpine(version);
                        versionSkipArm64[key] = definition.ShouldSkipArm64(version);
                    }

                    versionToFrameworks[key].Add(group.Framework.ToString());
                }
            }

            var entries = versionToFrameworks
                .OrderBy(kvp => new Version(kvp.Key))
                .Select(kvp => new VersionManifestEntry
                {
                    Version = kvp.Key,
                    Frameworks = kvp.Value.Distinct().OrderBy(f => f).ToList(),
                    SkipAlpine = versionSkipAlpine[kvp.Key],
                    SkipArm64 = versionSkipArm64[kvp.Key],
                })
                .ToList();

            if (entries.Count > 0)
            {
                manifest.Integrations[definition.IntegrationName] = new IntegrationManifestEntry
                {
                    Versions = entries,
                };
            }
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions);
    }

}

// ──────────────────────────────────────────────────
// Manifest DTOs (serialized to JSON, deserialized by tests)
// ──────────────────────────────────────────────────

public class PackageVersionManifest
{
    public DateTimeOffset GeneratedAt { get; set; }
    public Dictionary<string, IntegrationManifestEntry> Integrations { get; set; }
}

public class IntegrationManifestEntry
{
    public List<VersionManifestEntry> Versions { get; set; }
}

public class VersionManifestEntry
{
    public string Version { get; set; }
    public List<string> Frameworks { get; set; }
    public bool SkipAlpine { get; set; }
    public bool SkipArm64 { get; set; }
}
