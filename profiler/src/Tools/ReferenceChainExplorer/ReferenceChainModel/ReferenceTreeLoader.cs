// <copyright file="ReferenceTreeLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Text.Json;

namespace ReferenceChainModel;

/// <summary>
/// Loads a <see cref="ReferenceTree"/> from JSON content or a file path.
/// </summary>
public static class ReferenceTreeLoader
{
    /// <summary>
    /// Parse a reference tree from a JSON string.
    /// </summary>
    // The native profiler's MaxTreeDepth is 128, so the JSON can nest deeper than
    // the System.Text.Json default of 64. Use 256 to leave headroom.
    private static readonly JsonDocumentOptions JsonOptions = new() { MaxDepth = 256 };

    /// <summary>
    /// Parse a reference tree from a JSON string.
    /// </summary>
    public static ReferenceTree Load(string jsonContent)
    {
        var doc = JsonDocument.Parse(jsonContent, JsonOptions);
        return ParseDocument(doc);
    }

    /// <summary>
    /// Load a reference tree from a JSON file on disk.
    /// </summary>
    public static ReferenceTree LoadFromFile(string path)
    {
        var jsonContent = File.ReadAllText(path);
        return Load(jsonContent);
    }

    private static ReferenceTree ParseDocument(JsonDocument doc)
    {
        var root = doc.RootElement;

        int version = root.TryGetProperty("v", out var vProp) ? vProp.GetInt32() : 0;

        // Parse the type table ("tt" array)
        var typeTable = Array.Empty<string>();
        if (root.TryGetProperty("tt", out var ttProp))
        {
            typeTable = new string[ttProp.GetArrayLength()];
            int i = 0;
            foreach (var entry in ttProp.EnumerateArray())
            {
                typeTable[i++] = entry.GetString() ?? string.Empty;
            }
        }

        // Parse the roots array ("r")
        var roots = new List<ReferenceRootNode>();
        if (root.TryGetProperty("r", out var rProp))
        {
            foreach (var rootElement in rProp.EnumerateArray())
            {
                roots.Add(ParseRootNode(rootElement));
            }
        }

        return new ReferenceTree(version, typeTable, roots);
    }

    private static ReferenceRootNode ParseRootNode(JsonElement element)
    {
        int typeIndex = element.TryGetProperty("t", out var tProp) ? tProp.GetInt32() : -1;
        string categoryCode = element.TryGetProperty("c", out var cProp) ? (cProp.GetString() ?? "?") : "?";
        long instanceCount = element.TryGetProperty("ic", out var icProp) ? icProp.GetInt64() : 0;
        long totalSize = element.TryGetProperty("ts", out var tsProp) ? tsProp.GetInt64() : 0;

        var children = ParseChildren(element);

        return new ReferenceRootNode(typeIndex, instanceCount, totalSize, categoryCode, children);
    }

    private static ReferenceNode ParseNode(JsonElement element)
    {
        int typeIndex = element.TryGetProperty("t", out var tProp) ? tProp.GetInt32() : -1;
        long instanceCount = element.TryGetProperty("ic", out var icProp) ? icProp.GetInt64() : 0;
        long totalSize = element.TryGetProperty("ts", out var tsProp) ? tsProp.GetInt64() : 0;

        var children = ParseChildren(element);

        return new ReferenceNode(typeIndex, instanceCount, totalSize, children);
    }

    private static IReadOnlyList<ReferenceNode> ParseChildren(JsonElement element)
    {
        if (!element.TryGetProperty("ch", out var chProp))
        {
            return Array.Empty<ReferenceNode>();
        }

        var children = new List<ReferenceNode>();
        foreach (var childElement in chProp.EnumerateArray())
        {
            children.Add(ParseNode(childElement));
        }

        return children;
    }
}
