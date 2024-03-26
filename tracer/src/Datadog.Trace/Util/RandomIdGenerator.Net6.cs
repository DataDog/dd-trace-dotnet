// <copyright file="RandomIdGenerator.Net6.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Util;

/// <summary>
/// Generates random numbers suitable for use as Datadog trace ids and span ids.
/// </summary>
internal sealed class RandomIdGenerator : IRandomIdGenerator
{
    // On .NET 6+, we delegate to System.Random.Shared which can be safely accessed from
    // multiple threads and implements xoshiro128** or xoshiro256**.
    public static RandomIdGenerator Shared { get; } = new();

    /// <summary>
    /// Returns a random number that is greater than zero and less than or equal to UInt64.MaxValue.
    /// </summary>
    private static ulong NextNonZeroUInt64()
    {
        ulong result;

        // returns a value in the range [Int64.MinValue, Int64.MaxValue),
        var int64 = Random.Shared.NextInt64(long.MinValue, long.MaxValue);

        if (int64 >= 0)
        {
            // if zero or positive, add 1 to shift the range to (0, Int64.MaxValue]
            result = (ulong)int64 + 1;
        }
        else
        {
            // the negative numbers in range [Int64.MinValue, 0)
            // become (Int64.MaxValue, UInt64.MaxValue] when cast to ulong
            result = unchecked((ulong)int64);
        }

        // result is in range (0, UInt64.MaxValue]
        return result;
    }

    /// <summary>
    /// Returns a random number that is greater than zero
    /// and less than or equal to Int64.MaxValue (0x7fffffffffffffff).
    /// Used for backwards compatibility with tracers that parse ids as signed integers.
    /// </summary>
    private static ulong NextLegacyId()
    {
        // Random.NextInt64() returns a number in the range [0, Int64.MaxValue).
        // Add 1 to shift the range to (0, Int64.MaxValue].
        return (ulong)Random.Shared.NextInt64() + 1;
    }

    /// <inheritDoc />
    public ulong NextSpanId(bool useAllBits = false)
    {
        if (!useAllBits)
        {
            // get a value in the range (0, Int64.MaxValue]
            return NextLegacyId();
        }

        // get a value in the range (0, UInt64.MaxValue]
        return NextNonZeroUInt64();
    }

    /// <inheritDoc />
    public TraceId NextTraceId(bool useAllBits)
    {
        if (!useAllBits)
        {
            // get a value in the range (0, Int64.MaxValue]
            return (TraceId)NextLegacyId();
        }

        var seconds = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 128 bits = <32-bit unix seconds> <32 bits of zero> <64 random bits>
        var upper = (ulong)seconds << 32;
        var lower = NextNonZeroUInt64();

        return new TraceId(upper, lower);
    }
}

#endif
