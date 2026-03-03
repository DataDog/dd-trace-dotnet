// <copyright file="ReverseChainBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Builds reverse reference chains from the forward tree for a selected type.
/// Given a forward tree A → B → C, selecting C produces: C ← B ← A [root].
/// </summary>
public static class ReverseChainBuilder
{
    /// <summary>
    /// Build reverse chains for the given type index.
    /// Returns one <see cref="ReverseChainNode"/> per unique path position where the type appears.
    /// </summary>
    public static IReadOnlyList<ReverseChainNode> Build(ReferenceTree tree, int selectedTypeIndex)
    {
        // Build a parent adjacency map from the forward tree.
        var parentMap = new Dictionary<int, List<ParentInfo>>();
        foreach (var root in tree.Roots)
        {
            CollectParents(root, parentMap, isRoot: true, ((ReferenceRootNode)root).CategoryCode);
        }

        // Check if the type is a root with no parents referencing it
        if (!parentMap.ContainsKey(selectedTypeIndex))
        {
            foreach (var root in tree.Roots)
            {
                if (root.TypeIndex == selectedTypeIndex)
                {
                    return
                    [
                        new ReverseChainNode(
                            selectedTypeIndex,
                            root.InstanceCount,
                            root.TotalSize,
                            isRoot: true,
                            ((ReferenceRootNode)root).CategoryCode,
                            parents: Array.Empty<ReverseChainNode>())
                    ];
                }
            }

            return Array.Empty<ReverseChainNode>();
        }

        var visited = new HashSet<int>();
        var reverseNode = BuildReverseNode(selectedTypeIndex, parentMap, tree, visited);
        return reverseNode is not null ? [reverseNode] : Array.Empty<ReverseChainNode>();
    }

    private static void CollectParents(
        ReferenceNode node,
        Dictionary<int, List<ParentInfo>> parentMap,
        bool isRoot,
        string? categoryCode)
    {
        foreach (var child in node.Children)
        {
            if (!parentMap.TryGetValue(child.TypeIndex, out var parents))
            {
                parents = [];
                parentMap[child.TypeIndex] = parents;
            }

            // Avoid duplicate parent entries for the same parent type at this level
            bool alreadyRecorded = false;
            foreach (var existing in parents)
            {
                if (existing.TypeIndex == node.TypeIndex &&
                    existing.IsRoot == isRoot &&
                    existing.CategoryCode == categoryCode)
                {
                    alreadyRecorded = true;
                    break;
                }
            }

            if (!alreadyRecorded)
            {
                parents.Add(new ParentInfo(
                    node.TypeIndex,
                    node.InstanceCount,
                    node.TotalSize,
                    isRoot,
                    categoryCode));
            }

            // Recurse into children (they are not roots)
            CollectParents(child, parentMap, isRoot: false, categoryCode: null);
        }
    }

    private static ReverseChainNode? BuildReverseNode(
        int typeIndex,
        Dictionary<int, List<ParentInfo>> parentMap,
        ReferenceTree tree,
        HashSet<int> visited)
    {
        if (!visited.Add(typeIndex))
        {
            // Cycle guard: already expanding this type in the current reverse chain
            return null;
        }

        // Check if this type is itself a root
        long instanceCount = 0;
        long totalSize = 0;
        bool isRoot = false;
        string? categoryCode = null;

        foreach (var root in tree.Roots)
        {
            if (root.TypeIndex == typeIndex)
            {
                isRoot = true;
                categoryCode = ((ReferenceRootNode)root).CategoryCode;
                instanceCount += root.InstanceCount;
                totalSize += root.TotalSize;
            }
        }

        // Build parent chain
        var parentNodes = new List<ReverseChainNode>();
        if (parentMap.TryGetValue(typeIndex, out var parentInfos))
        {
            if (instanceCount == 0 && totalSize == 0)
            {
                // Not a root — aggregate from parent references
                foreach (var p in parentInfos)
                {
                    instanceCount += p.InstanceCount;
                    totalSize += p.TotalSize;
                }
            }

            foreach (var parentInfo in parentInfos)
            {
                if (parentInfo.IsRoot)
                {
                    parentNodes.Add(new ReverseChainNode(
                        parentInfo.TypeIndex,
                        parentInfo.InstanceCount,
                        parentInfo.TotalSize,
                        isRoot: true,
                        parentInfo.CategoryCode,
                        parents: Array.Empty<ReverseChainNode>()));
                }
                else
                {
                    var parentNode = BuildReverseNode(parentInfo.TypeIndex, parentMap, tree, visited);
                    if (parentNode is not null)
                    {
                        parentNodes.Add(parentNode);
                    }
                }
            }
        }

        visited.Remove(typeIndex); // Allow same type in different branches

        return new ReverseChainNode(
            typeIndex,
            instanceCount,
            totalSize,
            isRoot,
            categoryCode,
            parentNodes);
    }

    private readonly record struct ParentInfo(
        int TypeIndex,
        long InstanceCount,
        long TotalSize,
        bool IsRoot,
        string? CategoryCode);
}
