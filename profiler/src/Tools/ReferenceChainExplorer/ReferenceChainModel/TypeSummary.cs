// <copyright file="TypeSummary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Aggregated per-type statistics across all positions in the reference tree.
/// Contains only the type index — name resolution is deferred to the UI layer via converters.
/// </summary>
public class TypeSummary
{
    public TypeSummary(int typeIndex, long totalInstanceCount, long totalSize)
    {
        TypeIndex = typeIndex;
        TotalInstanceCount = totalInstanceCount;
        TotalSize = totalSize;
    }

    /// <summary>
    /// Index into <see cref="ReferenceTree.TypeTable"/>.
    /// Name resolution is deferred to the UI layer.
    /// </summary>
    public int TypeIndex { get; }

    public long TotalInstanceCount { get; }

    public long TotalSize { get; }

    /// <summary>
    /// Build a list of <see cref="TypeSummary"/> by walking the entire forward tree
    /// and aggregating instance counts and sizes per unique <see cref="ReferenceNode.TypeIndex"/>.
    /// </summary>
    public static IReadOnlyList<TypeSummary> BuildFromTree(ReferenceTree tree)
    {
        var aggregation = new Dictionary<int, TypeAggregation>();

        foreach (var root in tree.Roots)
        {
            AggregateNode(root, aggregation);
        }

        var summaries = new List<TypeSummary>(aggregation.Count);
        foreach (var (typeIndex, agg) in aggregation)
        {
            summaries.Add(new TypeSummary(typeIndex, agg.Count, agg.Size));
        }

        return summaries;
    }

    private static void AggregateNode(ReferenceNode node, Dictionary<int, TypeAggregation> aggregation)
    {
        if (node.TypeIndex < 0)
        {
            return;
        }

        if (aggregation.TryGetValue(node.TypeIndex, out var existing))
        {
            existing.Count += node.InstanceCount;
            existing.Size += node.TotalSize;
        }
        else
        {
            aggregation[node.TypeIndex] = new TypeAggregation { Count = node.InstanceCount, Size = node.TotalSize };
        }

        foreach (var child in node.Children)
        {
            AggregateNode(child, aggregation);
        }
    }

    private class TypeAggregation
    {
        public long Count { get; set; }

        public long Size { get; set; }
    }
}
