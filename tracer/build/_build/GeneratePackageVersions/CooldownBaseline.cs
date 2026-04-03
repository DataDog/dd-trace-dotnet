// <copyright file="CooldownBaseline.cs" company="Datadog">
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
/// Loads and saves the cooldown baseline file. The baseline records the max tested version
/// per integration, keyed by "{NuGetPackageName}/{IntegrationName}". This prevents cooldown
/// filtering from downgrading previously accepted versions.
///
/// Unlike the old system that derived baselines from supported_versions.json (with mismatched
/// granularity), this file is self-contained and keyed per integration name, so split-range
/// packages (e.g., GraphQL 4.x-6.x vs 7.x-9.x) each get their own baseline entry.
/// </summary>
public static class CooldownBaseline
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Loads the baseline from disk. Returns an empty dictionary if the file does not exist.
    /// </summary>
    public static async Task<Dictionary<string, Version>> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, Version>();
        }

        await using var stream = File.OpenRead(path);
        var raw = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream);

        if (raw is null)
        {
            return new Dictionary<string, Version>();
        }

        return raw
            .Where(kvp => Version.TryParse(kvp.Value, out _))
            .ToDictionary(kvp => kvp.Key, kvp => new Version(kvp.Value));
    }

    /// <summary>
    /// Saves the baseline to disk. Entries are sorted for stable diffs.
    /// </summary>
    public static async Task Save(string path, Dictionary<string, Version> baseline)
    {
        var sorted = baseline
            .OrderBy(kvp => kvp.Key)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, sorted, JsonOptions);
    }
}
