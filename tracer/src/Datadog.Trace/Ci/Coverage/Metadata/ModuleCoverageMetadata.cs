// <copyright file="ModuleCoverageMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage.Metadata;

/// <summary>
/// Module Coverage Metadata base class
/// </summary>
public abstract class ModuleCoverageMetadata
{
    /// <summary>
    /// Gets or sets the metadata array
    /// </summary>
    protected int[][] Metadata { get; set; } = Array.Empty<int[]>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetTotalTypes() => Metadata.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetTotalMethodsOfType(int type) => Metadata[type].Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetTotalSequencePointsOfMethod(int type, int method) => Metadata[type][method];
}
