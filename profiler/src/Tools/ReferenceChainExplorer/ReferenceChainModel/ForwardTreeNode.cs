// <copyright file="ForwardTreeNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Unified node type for the forward reference tree hierarchy.
/// Index-based: type names are resolved by the UI layer via converters.
/// Child nodes are created lazily when the parent is expanded.
/// </summary>
public class ForwardTreeNode
{
    private readonly Func<IReadOnlyList<ForwardTreeNode>>? _childrenFactory;
    private IReadOnlyList<ForwardTreeNode>? _children;

    /// <summary>
    /// Constructor for nodes with pre-computed children (e.g., category nodes).
    /// </summary>
    public ForwardTreeNode(
        ForwardTreeNodeKind kind,
        int typeIndex,
        string? categoryCode,
        long instanceCount,
        long totalSize,
        IReadOnlyList<ForwardTreeNode> children,
        string? fieldName = null,
        bool isMatchingFilter = false)
    {
        Kind = kind;
        TypeIndex = typeIndex;
        CategoryCode = categoryCode;
        InstanceCount = instanceCount;
        TotalSize = totalSize;
        FieldName = fieldName;
        IsMatchingFilter = isMatchingFilter;
        _children = children;
        _childrenFactory = null;
    }

    /// <summary>
    /// Constructor for nodes with lazy children. Children are created when first accessed.
    /// </summary>
    public ForwardTreeNode(
        ForwardTreeNodeKind kind,
        int typeIndex,
        string? categoryCode,
        long instanceCount,
        long totalSize,
        Func<IReadOnlyList<ForwardTreeNode>> childrenFactory,
        string? fieldName = null,
        bool isMatchingFilter = false)
    {
        Kind = kind;
        TypeIndex = typeIndex;
        CategoryCode = categoryCode;
        InstanceCount = instanceCount;
        TotalSize = totalSize;
        FieldName = fieldName;
        IsMatchingFilter = isMatchingFilter;
        _childrenFactory = childrenFactory;
    }

    /// <summary>
    /// Whether this is a category grouping, a root, or a chain child.
    /// </summary>
    public ForwardTreeNodeKind Kind { get; }

    /// <summary>
    /// Index into <see cref="ReferenceTree.TypeTable"/>. -1 for Category nodes.
    /// </summary>
    public int TypeIndex { get; }

    /// <summary>
    /// Root category code for Category and Root nodes (e.g., "K", "H").
    /// </summary>
    public string? CategoryCode { get; }

    /// <summary>
    /// For static root nodes: the name of the static field (e.g., "_staticOrders").
    /// Null for non-static roots and child nodes.
    /// </summary>
    public string? FieldName { get; }

    public long InstanceCount { get; }

    public long TotalSize { get; }

    /// <summary>
    /// True when the node's type matches the active filter text.
    /// Used by the UI to highlight matching nodes.
    /// </summary>
    public bool IsMatchingFilter { get; }

    /// <summary>
    /// Child nodes in the tree. Created lazily when first accessed (on parent expand).
    /// </summary>
    public IReadOnlyList<ForwardTreeNode> Children => _children ??= _childrenFactory?.Invoke() ?? Array.Empty<ForwardTreeNode>();
}
