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
    /// <summary>
    /// Gets or sets the metadata array
    /// </summary>
#pragma warning disable SA1401
    protected readonly long[] Metadata = Array.Empty<long>();
#pragma warning restore SA1401

    /// <summary>
    /// Gets or sets the metadata array
    /// </summary>
#pragma warning disable SA1401
    protected readonly int[] SequencePoints = Array.Empty<int>();
#pragma warning restore SA1401

    /// <summary>
    /// Gets the total instructions number
    /// </summary>
#pragma warning disable SA1401
    protected readonly long TotalInstructions = 0;
#pragma warning restore SA1401

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetMethodsCount() => Metadata.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetTotalSequencePointsOfMethod(int methodIndex)
    {
#if NETCOREAPP3_0_OR_GREATER
        return SequencePoints.FastGetReference(methodIndex);
#else
        return SequencePoints[methodIndex];
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GetMethodsMetadata(int methodIndex, out int typeIdx, out int methodIdx)
    {
#if NETCOREAPP3_0_OR_GREATER
        var methodMetadataIndexes = Metadata.FastGetReference(methodIndex);
#else
        var methodMetadataIndexes = Metadata[methodIndex];
#endif
        methodIdx = (int)(methodMetadataIndexes & 0xFFFF);
        typeIdx = (int)(methodMetadataIndexes >> 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long GetTotalInstructions() => TotalInstructions;
}
