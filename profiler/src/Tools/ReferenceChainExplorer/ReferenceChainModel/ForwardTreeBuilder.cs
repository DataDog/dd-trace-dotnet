// <copyright file="ForwardTreeBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Builds the forward reference tree grouped by root kind for display.
/// </summary>
public static class ForwardTreeBuilder
{
    /// <summary>
    /// Build the top-level forward tree nodes grouped by root category.
    /// Returns category nodes (Stack, Handle, etc.), each containing roots of that category.
    /// </summary>
    public static IReadOnlyList<ForwardTreeNode> Build(ReferenceTree tree) => BuildFiltered(tree, null);

    /// <summary>
    /// Build the forward tree, optionally filtering to only show paths that contain a type matching the filter.
    /// The filter is matched case-insensitively against short type names.
    /// </summary>
    public static IReadOnlyList<ForwardTreeNode> BuildFiltered(ReferenceTree tree, string? filter)
    {
        var matchingTypes = GetMatchingTypeIndices(tree, filter);

        var groups = new Dictionary<string, List<ReferenceRootNode>>(StringComparer.Ordinal);

        foreach (var root in tree.Roots)
        {
            if (matchingTypes is not null && !SubtreeContainsAny(root, matchingTypes))
            {
                continue;
            }

            var code = root.CategoryCode;
            if (!groups.TryGetValue(code, out var list))
            {
                list = new List<ReferenceRootNode>();
                groups[code] = list;
            }

            list.Add(root);
        }

        // Stable order: use known category order, then any unknowns
        var orderedCodes = new[] { "K", "S", "F", "H", "P", "W", "R", "O", "?" };
        var result = new List<ForwardTreeNode>();

        foreach (var code in orderedCodes)
        {
            if (groups.TryGetValue(code, out var roots) && roots.Count > 0)
            {
                var childNodes = roots.OrderByDescending(r => r.TotalSize)
                    .Select(r => WrapRoot(r, matchingTypes))
                    .ToList();
                result.Add(new ForwardTreeNode(
                    ForwardTreeNodeKind.Category,
                    typeIndex: -1,
                    code,
                    0,
                    0,
                    childNodes));
            }
        }

        // Include any categories not in the standard list
        foreach (var (code, roots) in groups.OrderBy(x => x.Key))
        {
            if (!orderedCodes.Contains(code))
            {
                var childNodes = roots.OrderByDescending(r => r.TotalSize)
                    .Select(r => WrapRoot(r, matchingTypes))
                    .ToList();
                result.Add(new ForwardTreeNode(
                    ForwardTreeNodeKind.Category,
                    typeIndex: -1,
                    code,
                    0,
                    0,
                    childNodes));
            }
        }

        return result;
    }

    private static HashSet<int>? GetMatchingTypeIndices(ReferenceTree tree, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var matching = new HashSet<int>();
        for (int i = 0; i < tree.TypeTable.Count; i++)
        {
            var shortName = tree.GetShortTypeName(i);
            if (shortName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                matching.Add(i);
            }
        }

        return matching;
    }

    private static bool SubtreeContainsAny(ReferenceNode node, HashSet<int> typeIndices)
    {
        if (typeIndices.Contains(node.TypeIndex))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (SubtreeContainsAny(child, typeIndices))
            {
                return true;
            }
        }

        return false;
    }

    private static ForwardTreeNode WrapRoot(ReferenceRootNode root, HashSet<int>? matchingTypes)
    {
        bool isMatch = matchingTypes is not null && matchingTypes.Contains(root.TypeIndex);
        return new ForwardTreeNode(
            ForwardTreeNodeKind.Root,
            root.TypeIndex,
            root.CategoryCode,
            root.InstanceCount,
            root.TotalSize,
            () => FilterAndWrapChildren(root.Children, matchingTypes, filterBranches: !isMatch),
            root.FieldName,
            isMatchingFilter: isMatch);
    }

    private static IReadOnlyList<ForwardTreeNode> FilterAndWrapChildren(
        IReadOnlyList<ReferenceNode> children,
        HashSet<int>? matchingTypes,
        bool filterBranches)
    {
        if (matchingTypes is null)
        {
            return children.Select(c => WrapNode(c, null)).ToList();
        }

        if (!filterBranches)
        {
            return children.Select(c => WrapNode(c, matchingTypes)).ToList();
        }

        return children
            .Where(c => SubtreeContainsAny(c, matchingTypes))
            .Select(c => WrapNodeFiltered(c, matchingTypes))
            .ToList();
    }

    private static ForwardTreeNode WrapNodeFiltered(ReferenceNode node, HashSet<int> matchingTypes)
    {
        bool isMatch = matchingTypes.Contains(node.TypeIndex);
        return new ForwardTreeNode(
            ForwardTreeNodeKind.Child,
            node.TypeIndex,
            null,
            node.InstanceCount,
            node.TotalSize,
            () => FilterAndWrapChildren(node.Children, matchingTypes, filterBranches: !isMatch),
            fieldName: null,
            isMatchingFilter: isMatch);
    }

    private static ForwardTreeNode WrapNode(ReferenceNode node, HashSet<int>? matchingTypes)
    {
        bool isMatch = matchingTypes is not null && matchingTypes.Contains(node.TypeIndex);
        return new ForwardTreeNode(
            ForwardTreeNodeKind.Child,
            node.TypeIndex,
            null,
            node.InstanceCount,
            node.TotalSize,
            () => node.Children.Select(c => WrapNode(c, matchingTypes)).ToList(),
            fieldName: null,
            isMatchingFilter: isMatch);
    }
}
