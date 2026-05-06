// <copyright file="ExplorationTestState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Debugger;

/// <summary>
/// Single canonical "exploration test is active" flag shared by all exploration-test
/// subsystems (metrics, probe tracker, etc.). Subsystems initialize their own state
/// first and then call <see cref="Activate"/>; production hot paths read
/// <see cref="IsActive"/> as a single volatile field check (zero-cost when disabled).
/// </summary>
internal static class ExplorationTestState
{
    private static int _isActive;

    public static bool IsActive => Volatile.Read(ref _isActive) == 1;

    /// <summary>
    /// Atomically activates the shared flag. Returns true on the first activation.
    /// </summary>
    internal static bool Activate() => Interlocked.CompareExchange(ref _isActive, 1, 0) == 0;

    /// <summary>
    /// Resets the shared flag (used by tests).
    /// </summary>
    internal static void Reset() => Volatile.Write(ref _isActive, 0);
}
