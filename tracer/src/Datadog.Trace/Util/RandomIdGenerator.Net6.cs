// <copyright file="RandomIdGenerator.Net6.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Util;

/// <summary>
/// Generates random numbers suitable for use as Datadog trace ids and span ids.
/// </summary>
internal sealed class RandomIdGenerator : IRandomIdGenerator
{
    // When true, all ID generation uses RandomNumberGenerator.Fill()
    // (reads kernel entropy on every call) instead of Random.Shared (PRNG
    // state that may be duplicated across process copies).
    private static readonly bool _secureRandom =
        EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.TraceSecureRandom) == "true";

    // On .NET 6+, we delegate to System.Random.Shared which can be safely accessed from
    // multiple threads and implements xoshiro128** or xoshiro256**.
    public static RandomIdGenerator Shared { get; } = new();

    /// <summary>
    /// Returns a random number that is greater than zero and less than or equal to UInt64.MaxValue.
    /// </summary>
    private static ulong NextNonZeroUInt64()
    {
        if (_secureRandom)
        {
            return NextNonZeroUInt64Secure();
        }

        ulong result2;

        // returns a value in the range [Int64.MinValue, Int64.MaxValue),
        var int64 = Random.Shared.NextInt64(long.MinValue, long.MaxValue);

        if (int64 >= 0)
        {
            // if zero or positive, add 1 to shift the range to (0, Int64.MaxValue]
            result2 = (ulong)int64 + 1;
        }
        else
        {
            // the negative numbers in range [Int64.MinValue, 0)
            // become (Int64.MaxValue, UInt64.MaxValue] when cast to ulong
            result2 = unchecked((ulong)int64);
        }

        // result is in range (0, UInt64.MaxValue]
        return result2;
    }

    /// <summary>
    /// Returns a random number that is greater than zero
    /// and less than or equal to Int64.MaxValue (0x7fffffffffffffff).
    /// Used for backwards compatibility with tracers that parse ids as signed integers.
    /// </summary>
    private static ulong NextLegacyId()
    {
        if (_secureRandom)
        {
            return (NextNonZeroUInt64() >> 1) | 1UL;
        }

        // Random.NextInt64() returns a number in the range [0, Int64.MaxValue).
        // Add 1 to shift the range to (0, Int64.MaxValue].
        return (ulong)Random.Shared.NextInt64() + 1;
    }

    /// <summary>
    /// No-op on .NET 6+: RandomNumberGenerator.Fill() reads from the kernel
    /// entropy pool on every call — no buffered PRNG state to reset.
    /// Provided for API symmetry with the pre-.NET 6 variant.
    /// </summary>
    public static void NotifyRestore()
    {
    }

    /// <summary>
    /// Always uses the CSPRNG path, regardless of <see cref="_secureRandom"/>.
    /// For testing only.
    /// </summary>
    internal static ulong NextSpanIdSecureForTesting(bool useAllBits)
    {
        if (!useAllBits)
        {
            return (NextNonZeroUInt64Secure() >> 1) | 1UL;
        }

        return NextNonZeroUInt64Secure();
    }

    /// <summary>
    /// Always uses the CSPRNG path, regardless of <see cref="_secureRandom"/>.
    /// For testing only.
    /// </summary>
    internal static TraceId NextTraceIdSecureForTesting(bool useAllBits)
    {
        if (!useAllBits)
        {
            return (TraceId)((NextNonZeroUInt64Secure() >> 1) | 1UL);
        }

        var seconds = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var upper = (ulong)seconds << 32;
        var lower = NextNonZeroUInt64Secure();
        return new TraceId(upper, lower);
    }

    private static ulong NextNonZeroUInt64Secure()
    {
        Span<byte> buf = stackalloc byte[8];
        ulong result;
        do
        {
            RandomNumberGenerator.Fill(buf);
            result = BitConverter.ToUInt64(buf);
        }
        while (result == 0);
        return result;
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
