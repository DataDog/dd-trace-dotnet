// <copyright file="CoverageScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        _sequencePoints[index]++;
    }

    /// <summary>
    /// Report a running instruction
    /// </summary>
    /// <param name="index">Sequence point index</param>
    /// <param name="index2">Second Sequence point index</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Report(int index, int index2)
    {
        _sequencePoints[index]++;
        _sequencePoints[index2]++;
    }
}
