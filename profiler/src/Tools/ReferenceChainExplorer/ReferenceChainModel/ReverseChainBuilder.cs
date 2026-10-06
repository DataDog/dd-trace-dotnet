// <copyright file="ReverseChainBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Indexes the forward tree and builds reverse reference chains for selected types.
/// Given chains A → B → C and F → G → C, selecting C produces:
/// C
/// ├── B → A [root]
/// └── G → F [root]
/// </summary>
public sealed class ReverseChainBuilder
{
    private static readonly string[] CategoryDisplayOrder = ["P", "H", "F", "K", "S", "W", "R", "O", "?"];

    private readonly List<Occurrence> _occurrences = [];
    private readonly Dictionary<int, List<int>> _occurrencesByType = [];
    private readonly Dictionary<int, (long Count, long Size)> _typeStats = [];

    public ReverseChainBuilder(ReferenceTree tree)
    {
        foreach (var root in tree.Roots)
        {
            IndexNode(root, parentIndex: -1, root.CategoryCode, root.FieldName);
        }

        TypeSummaries = _typeStats
                       .Select(entry => new TypeSummary(entry.Key, entry.Value.Count, entry.Value.Size))
                       .ToList();
    }

    /// <summary>
    /// Gets the per-type statistics collected while indexing the tree.
    /// </summary>
    public IReadOnlyList<TypeSummary> TypeSummaries { get; }

    /// <summary>
    /// Build reverse chains for the given type index.
    /// Returns one <see cref="ReverseChainNode"/> with all chains that reach the selected type, reversed.
    /// </summary>
    public IReadOnlyList<ReverseChainNode> Build(int selectedTypeIndex)
    {
        return _occurrencesByType.TryGetValue(selectedTypeIndex, out var occurrences)
                   ? [CreateNode(selectedTypeIndex, occurrences)]
                   : Array.Empty<ReverseChainNode>();
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

    private void IndexNode(
        ReferenceNode node,
        int parentIndex,
        string? categoryCode,
        string? fieldName)
    {
        int occurrenceIndex = _occurrences.Count;
        _occurrences.Add(new Occurrence(node.TypeIndex, parentIndex, categoryCode, fieldName));

        if (!_occurrencesByType.TryGetValue(node.TypeIndex, out var occurrences))
        {
            occurrences = [];
            _occurrencesByType[node.TypeIndex] = occurrences;
        }

        occurrences.Add(occurrenceIndex);

        if (_typeStats.TryGetValue(node.TypeIndex, out var existing))
        {
            _typeStats[node.TypeIndex] = (existing.Count + node.InstanceCount, existing.Size + node.TotalSize);
        }
        else
        {
            _typeStats[node.TypeIndex] = (node.InstanceCount, node.TotalSize);
        }

        foreach (var child in node.Children)
        {
            IndexNode(child, occurrenceIndex, categoryCode: null, fieldName: null);
        }
    }

    private ReverseChainNode CreateNode(
        int typeIndex,
        IReadOnlyList<int> occurrenceIndices)
    {
        bool isRoot = false;
        HashSet<string>? categoryCodes = null;
        string? fieldName = null;

        foreach (int occurrenceIndex in occurrenceIndices)
        {
            var occurrence = _occurrences[occurrenceIndex];
            if (occurrence.ParentIndex < 0)
            {
                isRoot = true;
                if (occurrence.CategoryCode is not null)
                {
                    categoryCodes ??= new HashSet<string>(StringComparer.Ordinal);
                    categoryCodes.Add(occurrence.CategoryCode);
                }

                fieldName ??= occurrence.FieldName;
            }
        }

        var categoryCode = categoryCodes is { Count: > 0 }
                               ? string.Join(",", OrderCategoriesForDisplay(categoryCodes))
                               : null;
        _typeStats.TryGetValue(typeIndex, out var stats);
        return new ReverseChainNode(
            typeIndex,
            stats.Count,
            stats.Size,
            isRoot,
            categoryCode,
            () => BuildParents(occurrenceIndices),
            fieldName);
    }

    private IReadOnlyList<ReverseChainNode> BuildParents(IReadOnlyList<int> occurrenceIndices)
    {
        var parentOccurrencesByType = new Dictionary<int, List<int>>();
        var seenParentOccurrences = new HashSet<int>();

        foreach (int occurrenceIndex in occurrenceIndices)
        {
            int parentIndex = _occurrences[occurrenceIndex].ParentIndex;
            if (parentIndex < 0 || !seenParentOccurrences.Add(parentIndex))
            {
                continue;
            }

            int parentTypeIndex = _occurrences[parentIndex].TypeIndex;
            if (!parentOccurrencesByType.TryGetValue(parentTypeIndex, out var parentOccurrences))
            {
                parentOccurrences = [];
                parentOccurrencesByType[parentTypeIndex] = parentOccurrences;
            }

            parentOccurrences.Add(parentIndex);
        }

        if (parentOccurrencesByType.Count == 0)
        {
            return Array.Empty<ReverseChainNode>();
        }

        var parents = new List<ReverseChainNode>(parentOccurrencesByType.Count);
        foreach (var (parentTypeIndex, parentOccurrences) in parentOccurrencesByType)
        {
            parents.Add(CreateNode(parentTypeIndex, parentOccurrences));
        }

        return parents;
    }

    private readonly record struct Occurrence(
        int TypeIndex,
        int ParentIndex,
        string? CategoryCode,
        string? FieldName);
}
