// <copyright file="PackageVersionResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Serilog;

namespace GeneratePackageVersions;

/// <summary>
/// Core orchestrator for resolving package versions. Takes integration definitions,
/// queries NuGet, applies filtering/cooldown/selection, and returns a pure data result.
/// No file I/O -- output generation is a separate concern.
/// </summary>
public class PackageVersionResolver
{
    private readonly Func<string, bool> _shouldResolve;
    private readonly int _cooldownDays;
    private readonly DateTimeOffset _cutoffDate;
    private readonly Dictionary<string, Version> _cooldownBaseline;

    // In-memory cache populated during this run. Keyed by NuGet package name.
    // Avoids redundant NuGet queries when multiple integrations share a package
    // (e.g., GraphQL 4.x and GraphQL 7.x both query "GraphQL").
    private readonly Dictionary<string, List<VersionWithDate>> _versionCache = new();

    public CooldownReport CooldownReport { get; private set; }

    /// <param name="shouldResolve">
    /// Controls which packages get re-resolved from NuGet. When this returns false
    /// for a package, that integration is marked as <see cref="IntegrationVersions.Unchanged"/>
    /// and the output generators preserve existing content for it.
    /// </param>
    public PackageVersionResolver(
        Func<string, bool> shouldResolve,
        int cooldownDays,
        Dictionary<string, Version> cooldownBaseline)
    {
        _shouldResolve = shouldResolve ?? (_ => true);
        _cooldownDays = cooldownDays;
        _cutoffDate = DateTimeOffset.UtcNow.AddDays(-cooldownDays);
        _cooldownBaseline = cooldownBaseline ?? new Dictionary<string, Version>();
        CooldownReport = new CooldownReport(cooldownDays);
    }

    public async Task<GenerationResult> Resolve(IReadOnlyList<IntegrationDefinition> definitions)
    {
        var integrations = new List<IntegrationVersions>();

        // When a package is being re-resolved, all integrations sharing the same
        // SampleProjectName must also be re-resolved. Otherwise the props output
        // (keyed by SampleName) would be incomplete -- e.g., AwsSdk and AwsDynamoDb
        // both use Samples.AWS.DynamoDBv2, so updating one without the other drops items.
        var resolvedSamples = new HashSet<string>();
        foreach (var def in definitions)
        {
            if (_shouldResolve(def.NuGetPackageName))
            {
                resolvedSamples.Add(def.SampleProjectName);
            }
        }

        foreach (var definition in definitions)
        {
            if (!_shouldResolve(definition.NuGetPackageName)
                && !resolvedSamples.Contains(definition.SampleProjectName))
            {
                // Not re-resolving this package and no sibling sharing its sample
                // is being resolved -- mark as unchanged so output generators
                // preserve existing content for this integration.
                integrations.Add(new IntegrationVersions
                {
                    Definition = definition,
                    LatestMinors = null,
                    LatestSpecific = null,
                    Unchanged = true,
                });
                continue;
            }

            var allVersions = await GetVersionsFromNuGet(definition.NuGetPackageName);
            var filtered = FilterToRange(allVersions, definition);

            var publishDates = filtered
                .GroupBy(v => v.Version)
                .ToDictionary(g => g.Key, g => g.First().Published);

            // Build the (version, framework) cross product, applying constraints
            var versionFrameworkPairs = (
                from v in filtered.Select(v => new Version(v.Version)).Distinct().OrderBy(v => v)
                from fw in definition.SupportedFrameworks
                where IsFrameworkAllowed(definition, v, fw)
                select (version: v, framework: fw))
                .ToList();

            // Apply cooldown
            versionFrameworkPairs = ApplyCooldown(definition, versionFrameworkPairs, publishDates);

            // Select versions for each strategy
            var latestMinors = SelectMax(versionFrameworkPairs, v => $"{v.Major}.{v.Minor}");
            var latestSpecific = definition.SpecificVersions.Length == 0
                ? SelectMax(versionFrameworkPairs, v => v.Major)
                : SelectFromGlobs(versionFrameworkPairs, definition.SpecificVersions);

            integrations.Add(new IntegrationVersions
            {
                Definition = definition,
                LatestMinors = latestMinors,
                LatestSpecific = latestSpecific,
            });
        }

        return new GenerationResult { Integrations = integrations };
    }

    /// <summary>
    /// Returns the max tested version per integration for the cooldown baseline file.
    /// For unchanged integrations, preserves the existing baseline entry.
    /// </summary>
    public Dictionary<string, Version> BuildNewBaseline(GenerationResult result)
    {
        var baseline = new Dictionary<string, Version>(_cooldownBaseline);
        foreach (var integration in result.Integrations)
        {
            if (integration.Unchanged)
            {
                continue;
            }

            var key = $"{integration.Definition.NuGetPackageName}/{integration.Definition.IntegrationName}";
            var allVersions = integration.LatestSpecific
                .SelectMany(g => g.Versions)
                .OrderByDescending(v => v)
                .ToList();

            if (allVersions.Count > 0)
            {
                baseline[key] = allVersions.First();
            }
        }

        return baseline;
    }

    private async Task<List<VersionWithDate>> GetVersionsFromNuGet(string packageName)
    {
        if (_versionCache.TryGetValue(packageName, out var cached))
        {
            return cached;
        }

        var versions = await QueryNuGet(packageName);
        _versionCache[packageName] = versions;
        return versions;
    }

    private static async Task<List<VersionWithDate>> QueryNuGet(string packageName)
    {
        var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());

        var repo = new SourceRepository(packageSource, providers);
        var resource = await repo.GetResourceAsync<PackageMetadataResource>();
        var cache = new SourceCacheContext();

        var metadata = await resource.GetMetadataAsync(
            packageName,
            includePrerelease: false,
            includeUnlisted: true,
            cache,
            NullLogger.Instance,
            System.Threading.CancellationToken.None);

        return metadata
            .Where(m => m.Identity.HasVersion)
            .Select(m => new VersionWithDate(
                m.Identity.Version.ToNormalizedString(),
                m.Published))
            .ToList();
    }

    private static List<VersionWithDate> FilterToRange(List<VersionWithDate> versions, IntegrationDefinition definition)
    {
        if (!NuGetVersion.TryParse(definition.MinVersion, out var min))
        {
            throw new ArgumentException($"Cannot parse MinVersion '{definition.MinVersion}' for {definition.IntegrationName}");
        }

        if (!NuGetVersion.TryParse(definition.MaxVersionExclusive, out var max))
        {
            throw new ArgumentException($"Cannot parse MaxVersionExclusive '{definition.MaxVersionExclusive}' for {definition.IntegrationName}");
        }

        return versions
            .Where(v => NuGetVersion.TryParse(v.Version, out var nv) && nv >= min && nv < max)
            .ToList();
    }

    private static bool IsFrameworkAllowed(IntegrationDefinition definition, Version version, TargetFramework framework)
    {
        foreach (var constraint in definition.Constraints)
        {
            if (!definition.IsVersionInConstraintRange(version, constraint))
            {
                continue;
            }

            if (constraint.OnlyFrameworks.Length > 0 && !constraint.OnlyFrameworks.Contains(framework))
            {
                return false;
            }

            if (constraint.ExcludeFrameworks.Length > 0 && constraint.ExcludeFrameworks.Contains(framework))
            {
                return false;
            }
        }

        return true;
    }

    private List<(Version version, TargetFramework framework)> ApplyCooldown(
        IntegrationDefinition definition,
        List<(Version version, TargetFramework framework)> pairs,
        Dictionary<string, DateTimeOffset?> publishDates)
    {
        var baselineKey = $"{definition.NuGetPackageName}/{definition.IntegrationName}";
        _cooldownBaseline.TryGetValue(baselineKey, out var baselineVersion);

        var result = new List<(Version version, TargetFramework framework)>();

        foreach (var (version, framework) in pairs)
        {
            publishDates.TryGetValue(version.ToString(), out var publishedDate);

            if (!IsWithinCooldown(publishedDate))
            {
                result.Add((version, framework));
                continue;
            }

            if (baselineVersion is not null && version <= baselineVersion)
            {
                result.Add((version, framework));
                continue;
            }

            // Excluded by cooldown. Find the best fallback for reporting.
            var fallback = pairs
                .Where(p => p.framework == framework && p.version < version)
                .Select(p => p.version)
                .OrderByDescending(v => v)
                .FirstOrDefault(v =>
                {
                    publishDates.TryGetValue(v.ToString(), out var d);
                    return !IsWithinCooldown(d) || (baselineVersion is not null && v <= baselineVersion);
                });

            CooldownReport.Add(new CooldownReport.CooldownEntry(
                definition.NuGetPackageName,
                definition.IntegrationName,
                version.ToString(),
                publishedDate,
                fallback?.ToString()));
        }

        return result;
    }

    private bool IsWithinCooldown(DateTimeOffset? publishedDate)
    {
        return publishedDate.HasValue && publishedDate.Value > _cutoffDate;
    }

    private static List<FrameworkVersionGroup> SelectMax<T>(
        List<(Version version, TargetFramework framework)> pairs,
        Func<Version, T> groupBy)
    {
        return pairs
            .GroupBy(p => p.framework)
            .Select(fwGroup => new FrameworkVersionGroup
            {
                Framework = fwGroup.Key,
                Versions = fwGroup
                    .Select(p => p.version)
                    .GroupBy(groupBy)
                    .Select(g => g.Max())
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList(),
            })
            .ToList();
    }

    private static List<FrameworkVersionGroup> SelectFromGlobs(
        List<(Version version, TargetFramework framework)> pairs,
        string[] globs)
    {
        return pairs
            .GroupBy(p => p.framework)
            .Select(fwGroup =>
            {
                var allVersions = fwGroup.Select(p => p.version).ToList();
                var selected = globs
                    .Select(glob => MatchGlob(glob, allVersions))
                    .Where(v => v is not null)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();

                return new FrameworkVersionGroup
                {
                    Framework = fwGroup.Key,
                    Versions = selected,
                };
            })
            .ToList();
    }

    private static Version MatchGlob(string glob, List<Version> versions)
    {
        var effectiveMin = new Version(glob.Replace("*", "0"));
        var effectiveMax = new Version(glob.Replace("*", "65535"));

        return versions
            .Where(v => v >= effectiveMin && v <= effectiveMax)
            .OrderByDescending(v => v)
            .FirstOrDefault();
    }
}

// ──────────────────────────────────────────────────
// Result types
// ──────────────────────────────────────────────────

public class GenerationResult
{
    public IReadOnlyList<IntegrationVersions> Integrations { get; init; }
}

public class IntegrationVersions
{
    public IntegrationDefinition Definition { get; init; }
    public List<FrameworkVersionGroup> LatestMinors { get; init; }
    public List<FrameworkVersionGroup> LatestSpecific { get; init; }

    /// <summary>
    /// When true, this integration was not re-resolved from NuGet (filtered by --IncludePackages/--ExcludePackages).
    /// Output generators should preserve existing content for this integration rather than overwriting it.
    /// </summary>
    public bool Unchanged { get; init; }
}

public class FrameworkVersionGroup
{
    public TargetFramework Framework { get; init; }
    public List<Version> Versions { get; init; }
}
