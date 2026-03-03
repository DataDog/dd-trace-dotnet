// <copyright file="ReverseChainNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// A node in a reverse chain: shows who references a given type, back up to the GC roots.
/// Contains only the type index — name resolution is deferred to the UI layer via converters.
/// </summary>
public class ReverseChainNode
{
    public ReverseChainNode(
        int typeIndex,
        long instanceCount,
        long totalSize,
        bool isRoot,
        string? categoryCode,
        IReadOnlyList<ReverseChainNode> parents)
    {
        TypeIndex = typeIndex;
        InstanceCount = instanceCount;
        TotalSize = totalSize;
        IsRoot = isRoot;
        CategoryCode = categoryCode;
        Parents = parents;
    }

    /// <summary>
    /// Index into <see cref="ReferenceTree.TypeTable"/>.
    /// Name resolution is deferred to the UI layer.
    /// </summary>
    public int TypeIndex { get; }

    public long InstanceCount { get; }

    public long TotalSize { get; }

    /// <summary>
    /// Whether this node is a GC root in the forward tree.
    /// </summary>
    public bool IsRoot { get; }

    /// <summary>
    /// Root category code ("S", "H", etc.), only set when <see cref="IsRoot"/> is true.
    /// </summary>
    public string? CategoryCode { get; }

    /// <summary>
    /// Parent nodes in the reverse chain (who references this type).
    /// </summary>
    public IReadOnlyList<ReverseChainNode> Parents { get; }
}
