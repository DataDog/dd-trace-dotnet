// <copyright file="PackageBumpReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneratePackageVersions;

/// <summary>
/// Collects the outcome of a package version regeneration run (packages that were
/// bumped and packages that were skipped by the cooldown filter) and renders them
/// as a markdown report for inclusion in the PR body.
/// </summary>
public class PackageBumpReport
{
    private readonly int _cooldownDays;
    private readonly List<BumpEntry> _bumpedEntries = new();
    private readonly List<CooldownEntry> _cooldownEntries = new();
    private readonly List<MajorAvailableEntry> _majorAvailableEntries = new();

    public PackageBumpReport(int cooldownDays)
    {
        _cooldownDays = cooldownDays;
    }

    public bool HasEntries => _bumpedEntries.Count > 0 || _cooldownEntries.Count > 0 || _majorAvailableEntries.Count > 0;

    public IReadOnlyList<BumpEntry> BumpedEntries => _bumpedEntries;

    public IReadOnlyList<CooldownEntry> CooldownEntries => _cooldownEntries;

    public IReadOnlyList<MajorAvailableEntry> MajorAvailableEntries => _majorAvailableEntries;

    public void AddBump(BumpEntry entry)
    {
        _bumpedEntries.Add(entry);
    }

    public void AddCooldown(CooldownEntry entry)
    {
        _cooldownEntries.Add(entry);
    }

    public void AddMajorAvailable(MajorAvailableEntry entry)
    {
        _majorAvailableEntries.Add(entry);
    }

    public string ToMarkdown()
    {
        if (!HasEntries)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        if (_bumpedEntries.Count > 0)
        {
            sb.AppendLine("## Package Version Updates");
            sb.AppendLine();
            sb.AppendLine("Bold entries indicate **major**-version bumps. `(new)` means no previous version existed.");
            sb.AppendLine();
            sb.AppendLine("| Package | Integration | Previous | New | Published |");
            sb.AppendLine("|---------|-------------|----------|-----|-----------|");

            foreach (var entry in _bumpedEntries)
            {
                var previous = entry.PreviousVersion?.ToString() ?? "(new)";
                var newVersion = entry.IsMajorBump ? $"**{entry.NewVersion}**" : entry.NewVersion.ToString();
                var published = entry.PublishedDate?.UtcDateTime.ToString("yyyy-MM-dd") ?? "unknown";

                sb.AppendLine($"| {entry.PackageName} | {entry.IntegrationName} | {previous} | {newVersion} | {published} |");
            }

            sb.AppendLine();
        }

        if (_majorAvailableEntries.Count > 0)
        {
            sb.AppendLine("## New Major Versions Available");
            sb.AppendLine();
            sb.AppendLine("These have a new major outside our supported range. Review whether to widen the");
            sb.AppendLine("integration's `MaximumVersion` and the definition's `MaxVersionExclusive`/`SpecificVersions`,");
            sb.AppendLine("or add a new split entry, to adopt.");
            sb.AppendLine();
            AppendMajorAvailableTable(sb, _majorAvailableEntries, "Latest available", e => e.LatestAvailable);
            sb.AppendLine();
        }

        if (_cooldownEntries.Count > 0)
        {
            sb.AppendLine("## Package Version Cooldown Report");
            sb.AppendLine();
            sb.AppendLine($"The following versions were published less than **{_cooldownDays} days** ago and have been overridden.");
            sb.AppendLine("These require manual review before inclusion.");
            sb.AppendLine();
            sb.AppendLine("| Package | Integration | Overridden Version | Published | Age (days) |");
            sb.AppendLine("|---------|-------------|--------------------|-----------|------------|");

            foreach (var entry in _cooldownEntries)
            {
                var published = entry.PublishedDate?.UtcDateTime.ToString("yyyy-MM-dd") ?? "unknown";
                var age = entry.PublishedDate.HasValue
                    ? ((int)(DateTimeOffset.UtcNow - entry.PublishedDate.Value).TotalDays).ToString()
                    : "?";

                sb.AppendLine($"| {entry.PackageName} | {entry.IntegrationName} | {entry.OverriddenVersion} | {published} | {age} |");
            }
        }

        return sb.ToString();
    }

    public async Task SaveToFile(string path)
    {
        var markdown = ToMarkdown();
        if (!string.IsNullOrEmpty(markdown))
        {
            // necessary to save to a file so that we can then output it in the PR description
            await File.WriteAllTextAsync(path, markdown);
        }
    }

    /// <summary>
    /// Renders the pending-majors file, merging this run's majors with preserved rows for packages it
    /// didn't inspect. Always returns content. See the Build.Utilities.cs call site for why it's committed.
    /// </summary>
    public string RenderPendingMajorsFile(string existingContent, ISet<string> inspectedPackages)
    {
        var merged = ParsePendingMajorsTable(existingContent)
            .Where(r => !inspectedPackages.Contains(r.PackageName))
            .Concat(_majorAvailableEntries)
            .OrderBy(r => r.PackageName, StringComparer.Ordinal)
            .ThenBy(r => r.IntegrationName, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Pending Major Versions");
        sb.AppendLine();
        sb.AppendLine("Integrations whose NuGet package has a new major outside the version range we currently test.");
        sb.AppendLine("Maintained automatically by the `GeneratePackageVersions` build target; do not edit by hand.");
        sb.AppendLine();

        if (merged.Count == 0)
        {
            sb.AppendLine("_None: every integration's tested range covers the latest major available on NuGet._");
            return sb.ToString();
        }

        AppendMajorAvailableTable(sb, merged, "Available major", e => e.LatestMajor);
        return sb.ToString();
    }

    /// <summary>
    /// Parses the data rows out of a previously-rendered pending-majors file, skipping the header and
    /// separator rows. Tolerates a missing or empty file (yields nothing).
    /// </summary>
    private static IEnumerable<MajorAvailableEntry> ParsePendingMajorsTable(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            yield break;
        }

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim('|').Split('|');
            if (cells.Length != 4)
            {
                continue;
            }

            var package = cells[0].Trim();

            // Skip the header row and the |---| separator row.
            if (package is "Package" || package.StartsWith('-'))
            {
                continue;
            }

            // The file stores the major form ("2.x") in the last column; the exact LatestAvailable
            // isn't persisted and isn't needed for preserved rows (the PR body renders only this run).
            var major = cells[3].Trim();
            yield return new MajorAvailableEntry(package, cells[1].Trim(), cells[2].Trim(), major, major);
        }
    }

    private static void AppendMajorAvailableTable(StringBuilder sb, IEnumerable<MajorAvailableEntry> entries, string lastHeader, Func<MajorAvailableEntry, string> lastValue)
    {
        sb.AppendLine($"| Package | Integration | Current cap | {lastHeader} |");
        sb.AppendLine("|---------|-------------|-------------|------------------|");

        foreach (var entry in entries)
        {
            sb.AppendLine($"| {entry.PackageName} | {entry.IntegrationName} | {entry.CurrentCap} | {lastValue(entry)} |");
        }
    }

    public record BumpEntry(
        string PackageName,
        string IntegrationName,
        Version PreviousVersion,
        Version NewVersion,
        DateTimeOffset? PublishedDate)
    {
        public bool IsMajorBump => PreviousVersion is null || NewVersion.Major > PreviousVersion.Major;
    }

    public record CooldownEntry(
        string PackageName,
        string IntegrationName,
        string OverriddenVersion,
        DateTimeOffset? PublishedDate);

    public record MajorAvailableEntry(
        string PackageName,
        string IntegrationName,
        string CurrentCap,
        string LatestAvailable,
        string LatestMajor);
}
