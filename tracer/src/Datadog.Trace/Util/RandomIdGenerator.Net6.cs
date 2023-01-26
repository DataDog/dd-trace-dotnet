// <copyright file="RandomIdGenerator.Net6.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Util;

/// <summary>
/// Generates random numbers suitable for use as Datadog trace ids and span ids.
/// </summary>
internal sealed class RandomIdGenerator
{
    // in .NET 6+, RandomIdGenerator is implemented using System.Random.Shared,
    // so it has no state itself and can be accessed safely from multiple thread (i.e. no threadstatic field)
    public static RandomIdGenerator Shared { get; } = new();

    /// <summary>
    /// Returns a random number that is greater than zero and less than or equal to Int64.MaxValue.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Match the API in other target frameworks.")]
    public ulong NextSpanId()
    {
        // On .NET 6+, System.Random.Shared uses xoshiro128** or xoshiro256**.
        // Random.NextInt64() returns a number in the range [0, Int64.MaxValue).
        // Add 1 to shift the range to (0, Int64.MaxValue].
        return (ulong)Random.Shared.NextInt64() + 1;
    }
}

#endif
