// <copyright file="CoverageScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Coverage scope
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref struct CoverageScope
{
    private readonly int[] _sequencePoints;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CoverageScope(int[] sequencePoints)
    {
        _sequencePoints = sequencePoints;
    }

    /// <summary>
    /// Report a running instruction
    /// </summary>
    /// <param name="index">Sequence point index</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Report(int index)
    {
#if NET5_0_OR_GREATER
        // Removes bound check
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_sequencePoints), index)++;
#else
        _sequencePoints[index]++;
#endif
    }

    /// <summary>
    /// Report a running instruction
    /// </summary>
    /// <param name="index">Sequence point index</param>
    /// <param name="index2">Second Sequence point index</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Report(int index, int index2)
    {
#if NET5_0_OR_GREATER
        // Removes bound check
        ref int arr = ref MemoryMarshal.GetArrayDataReference(_sequencePoints);
        Unsafe.Add(ref arr, index)++;
        Unsafe.Add(ref arr, index2)++;
#else
        _sequencePoints[index]++;
        _sequencePoints[index2]++;
#endif
    }
}
