// <copyright file="RootCategoryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Maps root category codes to human-readable display names.
/// </summary>
public static class RootCategoryHelper
{
    private static readonly Dictionary<string, string> CategoryNames = new()
    {
        ["K"] = "Stack",
        ["S"] = "StaticVariable",
        ["F"] = "Finalizer",
        ["H"] = "Handle",
        ["P"] = "Pinning",
        ["W"] = "ConditionalWeakTable",
        ["R"] = "COM",
        ["O"] = "Other",
        ["?"] = "Unknown",
    };

    private static readonly string[] CategoryDisplayOrder = ["P", "H", "F", "K", "S", "W", "R", "O", "?"];

    /// <summary>
    /// Get the human-readable name for a root category code.
    /// </summary>
    public static string GetCategoryName(string categoryCode)
    {
        return CategoryNames.TryGetValue(categoryCode, out var name) ? name : "Unknown";
    }

    /// <summary>
    /// Convert comma-separated category codes (e.g., "P,S") to human-readable names (e.g., "Pinning, StaticVariable").
    /// </summary>
    public static string GetCategoryNamesForDisplay(string categoryCodes)
    {
        if (string.IsNullOrEmpty(categoryCodes))
        {
            return string.Empty;
        }

        var codes = categoryCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ordered = codes
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c =>
            {
                var idx = Array.IndexOf(CategoryDisplayOrder, c);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .Select(GetCategoryName)
            .ToList();

        return string.Join(", ", ordered);
    }
}
