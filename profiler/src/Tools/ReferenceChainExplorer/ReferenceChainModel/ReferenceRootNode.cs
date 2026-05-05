// <copyright file="ReferenceRootNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// A root node in the forward reference tree. Extends <see cref="ReferenceNode"/> with a root category code.
/// </summary>
public class ReferenceRootNode : ReferenceNode
{
    public ReferenceRootNode(
        int typeIndex,
        long instanceCount,
        long totalSize,
        string categoryCode,
        string? fieldName,
        IReadOnlyList<ReferenceNode> children)
        : base(typeIndex, instanceCount, totalSize, children)
    {
        CategoryCode = categoryCode;
        FieldName = fieldName;
    }

    /// <summary>
    /// Root category code: "K" (stacK / Stack), "S" (Static), "F" (Finalizer),
    /// "H" (Handle), "P" (Pinning), "W" (ConditionalWeakTable), "R" (COM), "O" (Other), "?" (Unknown).
    /// </summary>
    public string CategoryCode { get; }

    /// <summary>
    /// For static roots: the name of the declaring static field (e.g., "_staticOrders").
    /// Null for non-static roots.
    /// </summary>
    public string? FieldName { get; }
}
