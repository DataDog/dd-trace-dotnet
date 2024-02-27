// <copyright file="FileCounter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// File counter struct
/// </summary>
public readonly unsafe ref struct FileCounter
{
    private readonly int* _counter;

    internal FileCounter(int* counter) => _counter = counter;

    /// <summary>
    /// Sets the line index as covered
    /// </summary>
    /// <param name="lineIndex">The line index</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int lineIndex)
    {
        _counter[lineIndex] = 1;
    }

    /// <summary>
    /// Sets the line index as covered
    /// </summary>
    /// <param name="lineIndex">The line index</param>
    /// <param name="increment">Increment value</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int lineIndex, int increment)
    {
        _counter[lineIndex] = increment;
    }
}
