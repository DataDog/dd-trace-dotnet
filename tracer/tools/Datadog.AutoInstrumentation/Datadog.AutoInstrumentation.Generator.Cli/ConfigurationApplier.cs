// <copyright file="ConfigurationApplier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Datadog.AutoInstrumentation.Generator.Core;

namespace Datadog.AutoInstrumentation.Generator.Cli;

/// <summary>
/// Applies --set key=value overrides to a <see cref="GenerationConfiguration"/> using reflection.
/// Keys use camelCase names matching the JSON output from --json.
/// </summary>
internal static class ConfigurationApplier
{
    private static readonly Dictionary<string, PropertyInfo> PropertyMap = BuildPropertyMap();

    /// <summary>
    /// Applies an array of "key=value" strings to the configuration.
    /// </summary>
    /// <returns>null on success, or an error message string.</returns>
    public static string? ApplyOverrides(GenerationConfiguration config, string[] setValues)
    {
        foreach (var entry in setValues)
        {
            var eqIndex = entry.IndexOf('=');
            if (eqIndex < 0)
            {
                return $"Error: Invalid --set format '{entry}'. Expected key=value (e.g., --set createDucktypeInstance=true).";
            }

            var key = entry[..eqIndex].Trim();
            var value = entry[(eqIndex + 1)..].Trim();

            if (!PropertyMap.TryGetValue(key.ToLowerInvariant(), out var prop))
            {
                var suggestion = FindClosestKey(key);
                var suggestionText = suggestion is not null ? $" Did you mean '{suggestion}'?" : string.Empty;
                return $"Error: Unknown configuration key '{key}'.{suggestionText} Use --list-keys to see available keys.";
            }

            if (prop.PropertyType == typeof(bool))
            {
                if (!bool.TryParse(value, out var boolValue))
                {
                    return $"Error: Invalid value '{value}' for key '{key}'. Expected true or false.";
                }

                prop.SetValue(config, boolValue);
            }
            else
            {
                return $"Error: Unsupported property type '{prop.PropertyType.Name}' for key '{key}'.";
            }
        }

        return null;
    }

    /// <summary>
    /// Returns all available configuration keys with their current default values.
    /// </summary>
    public static IEnumerable<(string Key, string Type, object? DefaultValue)> GetAvailableKeys()
    {
        var defaults = new GenerationConfiguration();
        foreach (var (key, prop) in PropertyMap.OrderBy(kv => kv.Key))
        {
            yield return (prop.Name[..1].ToLowerInvariant() + prop.Name[1..], prop.PropertyType.Name, prop.GetValue(defaults));
        }
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap()
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in typeof(GenerationConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite)
            {
                continue;
            }

            // Use camelCase key (same as JsonNamingPolicy.CamelCase)
            var camelKey = prop.Name[..1].ToLowerInvariant() + prop.Name[1..];
            map[camelKey.ToLowerInvariant()] = prop;
        }

        return map;
    }

    private static string? FindClosestKey(string input)
    {
        var inputLower = input.ToLowerInvariant();
        string? bestMatch = null;
        var bestDistance = int.MaxValue;

        foreach (var (key, prop) in PropertyMap)
        {
            var camelKey = prop.Name[..1].ToLowerInvariant() + prop.Name[1..];
            var distance = LevenshteinDistance(inputLower, key);
            if (distance < bestDistance && distance <= Math.Max(3, key.Length / 3))
            {
                bestDistance = distance;
                bestMatch = camelKey;
            }
        }

        return bestMatch;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
        {
            dp[i, 0] = i;
        }

        for (var j = 0; j <= n; j++)
        {
            dp[0, j] = j;
        }

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[m, n];
    }
}
