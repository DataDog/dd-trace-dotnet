// <copyright file="CooldownReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GeneratePackageVersions;

/// <summary>
/// Collects package versions that were excluded by the cooldown filter
/// and renders them as a markdown report for inclusion in the PR body.
/// </summary>
public class CooldownReport
{
    private readonly int _cooldownDays;
    private readonly List<CooldownEntry> _entries = new();

    public CooldownReport(int cooldownDays)
    {
        _cooldownDays = cooldownDays;
    }

    public bool HasEntries => _entries.Count > 0;

    public IReadOnlyList<CooldownEntry> Entries => _entries;

    public void Add(CooldownEntry entry)
    {
        _entries.Add(entry);
    }

    public string ToMarkdown()
    {
        if (_entries.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Package Version Cooldown Report");
        sb.AppendLine();
        sb.AppendLine($"The following versions were published less than **{_cooldownDays} days** ago and have been overridden.");
        sb.AppendLine("These require manual review before inclusion.");
        sb.AppendLine();
        sb.AppendLine("| Package | Integration | Overridden Version | Published | Age (days) |");
        sb.AppendLine("|---------|-------------|--------------------|-----------|------------|");

        foreach (var entry in _entries)
        {
            var published = entry.PublishedDate?.UtcDateTime.ToString("yyyy-MM-dd") ?? "unknown";
            var age = entry.PublishedDate.HasValue
                ? ((int)(DateTimeOffset.UtcNow - entry.PublishedDate.Value).TotalDays).ToString()
                : "?";

            sb.AppendLine($"| {entry.PackageName} | {entry.IntegrationName} | {entry.OverriddenVersion} | {published} | {age} |");
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

    public record CooldownEntry(
        string PackageName,
        string IntegrationName,
        string OverriddenVersion,
        DateTimeOffset? PublishedDate);
}
