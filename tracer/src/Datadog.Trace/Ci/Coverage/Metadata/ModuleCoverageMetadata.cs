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
    protected readonly int[][] Metadata = Array.Empty<int[]>();
#pragma warning restore SA1401

    /// <summary>
    /// Gets the total instructions number
    /// </summary>
#pragma warning disable SA1401
    public readonly long TotalInstructions;
#pragma warning restore SA1401

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetTotalTypes() => Metadata.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetTotalMethodsOfType(int type)
    {
#if NETCOREAPP3_0_OR_GREATER
        return Metadata.FastGetReference(type).Length;
#else
        return Metadata[type].Length;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetTotalSequencePointsOfMethod(int type, int method)
    {
#if NETCOREAPP3_0_OR_GREATER
        return Metadata.FastGetReference(type).FastGetReference(method);
#else
        return Metadata[type][method];
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GetTotalMethodsAndSequencePointsOfMethod(int type, int method, out int totalMethods, out int totalSequencePoints)
    {
#if NETCOREAPP3_0_OR_GREATER
        var typeMeta = Metadata.FastGetReference(type);
        totalMethods = typeMeta.Length;
        totalSequencePoints = typeMeta.FastGetReference(method);
#else
        var typeMeta = Metadata[type];
        totalMethods = typeMeta.Length;
        totalSequencePoints = typeMeta[method];
#endif
    }
}
