// <copyright file="ModuleCoverageMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci.Coverage.Util;

namespace Datadog.Trace.Ci.Coverage.Metadata;

/// <summary>
/// Module Coverage Metadata base class
/// </summary>
public abstract class ModuleCoverageMetadata
{
#pragma warning disable SA1401
    /// <summary>
    /// Gets or sets the total lines in the module
    /// </summary>
    public readonly int TotalLines;

    /// <summary>
    /// Gets or sets the coverage mode
    /// </summary>
    public readonly int CoverageMode;

    /// <summary>
    /// Gets or sets the file metadata array
    /// </summary>
    protected internal readonly FileCoverageMetadata[] Files = Array.Empty<FileCoverageMetadata>();
#pragma warning restore SA1401

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetOffset(int fileIndex)
    {
#if NETCOREAPP3_0_OR_GREATER
        return Files.FastGetReference(fileIndex).Offset;
#else
        return Files[fileIndex].Offset;
#endif
    }
}
