// <copyright file="NuGetVersionCache.cs" company="Datadog">
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
/// Manages a cache of NuGet package version lists so that excluded packages
/// can reuse previously fetched data instead of querying NuGet again.
/// </summary>
public static class NuGetVersionCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Load the version cache from disk. Returns an empty dictionary if the file doesn't exist.
    /// </summary>
    public static async Task<Dictionary<string, List<VersionWithDate>>> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, List<VersionWithDate>>();
        }

        await using var openStream = File.OpenRead(path);

        var result = await JsonSerializer.DeserializeAsync<List<KeyValuePair<string, List<VersionWithDate>>>>(openStream, JsonOptions)
                     ?? new List<KeyValuePair<string, List<VersionWithDate>>>();

        return new Dictionary<string, List<VersionWithDate>>(result);
    }

    /// <summary>
    /// Save the version cache to disk.
    /// </summary>
    public static async Task Save(string path, Dictionary<string, List<VersionWithDate>> cache)
    {
        // convert to a list to make sure it has deterministic ordering
        var ordered = cache
            .OrderBy(x => x.Key)
            .Select(x => new KeyValuePair<string, IEnumerable<VersionWithDate>>(
                x.Key,
                x.Value
                    .Select(v => v with { Version = Version.Parse(v.Version).ToString() })
                    .OrderBy(v => Version.Parse(v.Version))));
        await using var createStream = File.Create(path);
        await JsonSerializer.SerializeAsync(createStream, ordered, JsonOptions);
    }
}
