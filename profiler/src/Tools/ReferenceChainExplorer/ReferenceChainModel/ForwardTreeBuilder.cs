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
    public static IReadOnlyList<ForwardTreeNode> Build(ReferenceTree tree)
    {
        var groups = new Dictionary<string, List<ReferenceRootNode>>(StringComparer.Ordinal);

        foreach (var root in tree.Roots)
        {
            var code = root.CategoryCode;
            if (!groups.TryGetValue(code, out var list))
            {
                list = new List<ReferenceRootNode>();
                groups[code] = list;
            }

            list.Add(root);
        }

        // Stable order: use known category order, then any unknowns
        var orderedCodes = new[] { "S", "s", "F", "H", "P", "W", "R", "?" };
        var result = new List<ForwardTreeNode>();

        foreach (var code in orderedCodes)
        {
            if (groups.TryGetValue(code, out var roots) && roots.Count > 0)
            {
                var childNodes = roots.OrderByDescending(r => r.TotalSize).Select(r => WrapRoot(r)).ToList();
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
                var childNodes = roots.OrderByDescending(r => r.TotalSize).Select(r => WrapRoot(r)).ToList();
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

    private static ForwardTreeNode WrapRoot(ReferenceRootNode root)
    {
        return new ForwardTreeNode(
            ForwardTreeNodeKind.Root,
            root.TypeIndex,
            root.CategoryCode,
            root.InstanceCount,
            root.TotalSize,
            () => root.Children.Select(WrapNode).ToList(),
            root.FieldName);
    }

    private static ForwardTreeNode WrapNode(ReferenceNode node)
    {
        return new ForwardTreeNode(
            ForwardTreeNodeKind.Child,
            node.TypeIndex,
            null,
            node.InstanceCount,
            node.TotalSize,
            () => node.Children.Select(WrapNode).ToList());
    }
}
