// <copyright file="ForwardTreeNodeKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace ReferenceChainModel;

/// <summary>
/// Kind of node in the forward reference tree.
/// </summary>
public enum ForwardTreeNodeKind
{
    /// <summary>
    /// Top-level category grouping (e.g., Stack, Handle).
    /// </summary>
    Category,

    /// <summary>
    /// A GC root node.
    /// </summary>
    Root,

    /// <summary>
    /// A child node in the reference chain.
    /// </summary>
    Child,
}
