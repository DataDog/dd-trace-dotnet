// <copyright file="CooldownReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SmokeTests;

/// <summary>
/// Collects smoke-test image digests that were skipped by the cooldown filter
/// and renders them as a markdown report for inclusion in the PR body.
/// </summary>
public class CooldownReport
{
    readonly TimeSpan _cooldown;
    readonly List<CooldownEntry> _cooldownEntries = new();
    readonly List<FailureEntry> _failureEntries = new();

    public CooldownReport(TimeSpan cooldown)
    {
        _cooldown = cooldown;
    }

    public bool HasEntries => _cooldownEntries.Count > 0 || _failureEntries.Count > 0;

    public IReadOnlyList<CooldownEntry> Entries => _cooldownEntries;

    public IReadOnlyList<FailureEntry> Failures => _failureEntries;

    public void Add(CooldownEntry entry)
    {
        _cooldownEntries.Add(entry);
    }

    public void AddFailure(FailureEntry entry)
    {
        _failureEntries.Add(entry);
    }

    public string ToMarkdown()
    {
        if (!HasEntries)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        if (_cooldownEntries.Count > 0)
        {
            sb.AppendLine("## Smoke Test Image Digest Cooldown Report");
            sb.AppendLine();
            sb.AppendLine($"The following images have newer digests available but were published less than **{(int)_cooldown.TotalDays} day(s)** ago, so the existing pin is retained.");
            sb.AppendLine("They will be picked up automatically by a future run once they age out of the cooldown window.");
            sb.AppendLine();
            sb.AppendLine("| Image | Current pinned | Available digest | Published | Age |");
            sb.AppendLine("|-------|----------------|------------------|-----------|-----|");

            foreach (var entry in _cooldownEntries)
            {
                var published = entry.PublishedDate?.UtcDateTime.ToString("yyyy-MM-dd") ?? "unknown";
                var age = entry.PublishedDate.HasValue
                    ? FormatAge(DateTimeOffset.UtcNow - entry.PublishedDate.Value)
                    : "?";

                sb.AppendLine($"| `{entry.Image}` | `{Shorten(entry.CurrentDigest)}` | `{Shorten(entry.AvailableDigest)}` | {published} | {age} |");
            }
        }

        if (_failureEntries.Count > 0)
        {
            if (_cooldownEntries.Count > 0)
            {
                sb.AppendLine();
            }
            sb.AppendLine("## Smoke Test Image Digest Failures");
            sb.AppendLine();
            sb.AppendLine("The following images could not be evaluated this run. The existing pin has been kept. Investigate before the next scheduled run if these persist.");
            sb.AppendLine();
            sb.AppendLine("| Image | Error |");
            sb.AppendLine("|-------|-------|");

            foreach (var entry in _failureEntries)
            {
                sb.AppendLine($"| `{entry.Image}` | {EscapeTableCell(entry.ErrorMessage)} |");
            }
        }

        return sb.ToString();
    }

    public async Task SaveToFile(string path)
    {
        var markdown = ToMarkdown();
        if (!string.IsNullOrEmpty(markdown))
        {
            // Saved to disk so it can later be fed into the PR description of the automation workflow.
            await File.WriteAllTextAsync(path, markdown);
        }
    }

    static string EscapeTableCell(string value)
    {
        // Collapse newlines and escape the pipe so a single error string can't blow up the table.
        return value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("|", "\\|");
    }

    static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1)
        {
            return $"{(int)age.TotalDays}d";
        }
        return $"{(int)age.TotalHours}h";
    }

    static string Shorten(string digest)
    {
        var colon = digest.IndexOf(':');
        var hex = colon >= 0 ? digest[(colon + 1)..] : digest;
        return hex.Length > 12 ? hex.Substring(0, 12) : hex;
    }

    public record CooldownEntry(
        string Image,
        string CurrentDigest,
        string AvailableDigest,
        DateTimeOffset? PublishedDate);

    public record FailureEntry(
        string Image,
        string ErrorMessage);
}
