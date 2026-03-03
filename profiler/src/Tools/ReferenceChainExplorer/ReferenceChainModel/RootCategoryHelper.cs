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
        ["S"] = "Stack",
        ["s"] = "StaticVariable",
        ["F"] = "Finalizer",
        ["H"] = "Handle",
        ["P"] = "Pinning",
        ["W"] = "ConditionalWeakTable",
        ["R"] = "COM",
        ["?"] = "Unknown",
    };

    /// <summary>
    /// Get the human-readable name for a root category code.
    /// </summary>
    public static string GetCategoryName(string categoryCode)
    {
        return CategoryNames.TryGetValue(categoryCode, out var name) ? name : "Unknown";
    }
}
