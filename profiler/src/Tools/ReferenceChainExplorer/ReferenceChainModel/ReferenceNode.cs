// <copyright file="ReferenceNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// A node in the forward reference tree. Stores only an index into the type table.
/// </summary>
public class ReferenceNode
{
    public ReferenceNode(int typeIndex, long instanceCount, long totalSize, IReadOnlyList<ReferenceNode> children)
    {
        TypeIndex = typeIndex;
        InstanceCount = instanceCount;
        TotalSize = totalSize;
        Children = children;
    }

    /// <summary>
    /// Index into <see cref="ReferenceTree.TypeTable"/>. No string stored in the node.
    /// </summary>
    public int TypeIndex { get; }

    public long InstanceCount { get; }

    public long TotalSize { get; }

    public IReadOnlyList<ReferenceNode> Children { get; }
}
