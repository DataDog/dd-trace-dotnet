// <copyright file="ReverseChainBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Builds reverse reference chains from the forward tree for a selected type.
/// Given chains A → B → C and F → G → C, selecting C produces:
/// C
/// ├── B → A [root]
/// └── G → F [root]
/// </summary>
public static class ReverseChainBuilder
{
    private static readonly string[] CategoryDisplayOrder = ["P", "H", "F", "K", "S", "W", "R", "O", "?"];

    /// <summary>
    /// Build reverse chains for the given type index.
    /// Returns one <see cref="ReverseChainNode"/> with all chains that reach the selected type, reversed.
    /// </summary>
    public static IReadOnlyList<ReverseChainNode> Build(ReferenceTree tree, int selectedTypeIndex)
    {
        // Build a parent adjacency map from the forward tree.
        var parentMap = new Dictionary<int, List<ParentInfo>>();
        var typeStats = new Dictionary<int, (long Count, long Size)>();
        foreach (var root in tree.Roots)
        {
            var rootNode = (ReferenceRootNode)root;
            CollectParents(root, parentMap, isRoot: true, rootNode.CategoryCode, rootNode.FieldName);
            AggregateTypeStats(root, typeStats);
        }

        // Check if the type is a root with no parents referencing it
        if (!parentMap.ContainsKey(selectedTypeIndex))
        {
            var categoryCodes = new HashSet<string>(StringComparer.Ordinal);
            string? fieldName = null;

            foreach (var root in tree.Roots)
            {
                if (root.TypeIndex == selectedTypeIndex)
                {
                    var rootNode = (ReferenceRootNode)root;
                    categoryCodes.Add(rootNode.CategoryCode);
                    fieldName ??= rootNode.FieldName;
                }
            }

            if (categoryCodes.Count > 0)
            {
                typeStats.TryGetValue(selectedTypeIndex, out var stats);
                var allCategories = string.Join(",", OrderCategoriesForDisplay(categoryCodes));
                return
                [
                    new ReverseChainNode(
                        selectedTypeIndex,
                        stats.Count,
                        stats.Size,
                        isRoot: true,
                        allCategories,
                        parents: Array.Empty<ReverseChainNode>(),
                        fieldName)
                ];
            }

            return Array.Empty<ReverseChainNode>();
        }

        var visited = new HashSet<int>();
        var cache = new Dictionary<int, ReverseChainNode>();
        var reverseNode = BuildReverseNode(selectedTypeIndex, parentMap, tree, typeStats, visited, cache);
        return reverseNode is not null ? [reverseNode] : Array.Empty<ReverseChainNode>();
    }

    private static void CollectParents(
        ReferenceNode node,
        Dictionary<int, List<ParentInfo>> parentMap,
        bool isRoot,
        string? categoryCode,
        string? fieldName)
    {
        foreach (var child in node.Children)
        {
            if (!parentMap.TryGetValue(child.TypeIndex, out var parents))
            {
                parents = [];
                parentMap[child.TypeIndex] = parents;
            }

            // Avoid duplicate parent entries for the same parent type.
            // When a type appears as both a root and a non-root parent (e.g.,
            // ReferenceChainScenarios is a direct root AND a child of ComputerService),
            // prefer the non-root entry because its reverse chain will provide the
            // full path up to the actual root.
            int existingIdx = -1;
            for (int i = 0; i < parents.Count; i++)
            {
                if (parents[i].TypeIndex == node.TypeIndex)
                {
                    existingIdx = i;
                    break;
                }
            }

            if (existingIdx >= 0)
            {
                if (parents[existingIdx].IsRoot && !isRoot)
                {
                    // Replace root entry with non-root entry (richer chain context)
                    parents[existingIdx] = new ParentInfo(
                        node.TypeIndex,
                        node.InstanceCount,
                        node.TotalSize,
                        isRoot,
                        categoryCode,
                        fieldName);
                }
            }
            else
            {
                parents.Add(new ParentInfo(
                    node.TypeIndex,
                    node.InstanceCount,
                    node.TotalSize,
                    isRoot,
                    categoryCode,
                    fieldName));
            }

            // Recurse into children (they are not roots)
            CollectParents(child, parentMap, isRoot: false, categoryCode: null, fieldName: null);
        }
    }

    private static ReverseChainNode? BuildReverseNode(
        int typeIndex,
        Dictionary<int, List<ParentInfo>> parentMap,
        ReferenceTree tree,
        Dictionary<int, (long Count, long Size)> typeStats,
        HashSet<int> visited,
        Dictionary<int, ReverseChainNode> cache)
    {
        // Cycle guard: type already in current path
        if (!visited.Add(typeIndex))
        {
            return null;
        }

        // Memoization: reuse cached node if safe (would not create a cycle)
        if (cache.TryGetValue(typeIndex, out var cached))
        {
            visited.Remove(typeIndex);
            if (!SubtreeContainsAny(cached, visited))
            {
                return cached;
            }

            visited.Add(typeIndex); // Restore for build path
        }

        // Check if this type is itself a root (may have multiple categories: Pinning, StaticVariable, etc.)
        bool isRoot = false;
        var categoryCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in tree.Roots)
        {
            if (root.TypeIndex == typeIndex)
            {
                isRoot = true;
                categoryCodes.Add(((ReferenceRootNode)root).CategoryCode);
            }
        }

        var categoryCode = categoryCodes.Count > 0 ? string.Join(",", OrderCategoriesForDisplay(categoryCodes)) : null;

        // Use the pre-computed type stats (aggregated from the entire forward tree)
        typeStats.TryGetValue(typeIndex, out var stats);
        long instanceCount = stats.Count;
        long totalSize = stats.Size;

        // Build parent chain: one node per unique parent type (DAG, not tree)
        var parentNodes = new List<ReverseChainNode>();
        var seenNonRootTypes = new HashSet<int>();

        if (parentMap.TryGetValue(typeIndex, out var parentInfos))
        {
            foreach (var parentInfo in parentInfos)
            {
                if (parentInfo.IsRoot)
                {
                    typeStats.TryGetValue(parentInfo.TypeIndex, out var parentStats);
                    parentNodes.Add(new ReverseChainNode(
                        parentInfo.TypeIndex,
                        parentStats.Count,
                        parentStats.Size,
                        isRoot: true,
                        parentInfo.CategoryCode,
                        parents: Array.Empty<ReverseChainNode>(),
                        parentInfo.FieldName));
                }
                else if (seenNonRootTypes.Add(parentInfo.TypeIndex))
                {
                    var parentNode = BuildReverseNode(parentInfo.TypeIndex, parentMap, tree, typeStats, visited, cache);
                    if (parentNode is not null)
                    {
                        parentNodes.Add(parentNode);
                    }
                }
            }
        }

        visited.Remove(typeIndex);

        var node = new ReverseChainNode(
            typeIndex,
            instanceCount,
            totalSize,
            isRoot,
            categoryCode,
            parentNodes);
        cache[typeIndex] = node;
        return node;
    }

    /// <summary>
    /// Order category codes for display: Pinning, Handle, Finalizer, Stack, StaticVariable, etc.
    /// </summary>
    private static IEnumerable<string> OrderCategoriesForDisplay(IEnumerable<string> codes)
    {
        return codes.OrderBy(c =>
        {
            var idx = Array.IndexOf(CategoryDisplayOrder, c);
            return idx >= 0 ? idx : int.MaxValue;
        });
    }

    /// <summary>
    /// Returns true if the node's subtree (including self and all descendants) contains any type in the set.
    /// </summary>
    private static bool SubtreeContainsAny(ReverseChainNode node, HashSet<int> types)
    {
        if (types.Contains(node.TypeIndex))
        {
            return true;
        }

        foreach (var parent in node.Parents)
        {
            if (SubtreeContainsAny(parent, types))
            {
                return true;
            }
        }

        return false;
    }

    private static void AggregateTypeStats(ReferenceNode node, Dictionary<int, (long Count, long Size)> stats)
    {
        if (node.TypeIndex >= 0)
        {
            if (stats.TryGetValue(node.TypeIndex, out var existing))
            {
                stats[node.TypeIndex] = (existing.Count + node.InstanceCount, existing.Size + node.TotalSize);
            }
            else
            {
                stats[node.TypeIndex] = (node.InstanceCount, node.TotalSize);
            }
        }

        foreach (var child in node.Children)
        {
            AggregateTypeStats(child, stats);
        }
    }

    private readonly record struct ParentInfo(
        int TypeIndex,
        long InstanceCount,
        long TotalSize,
        bool IsRoot,
        string? CategoryCode,
        string? FieldName);
}
