// <copyright file="RandomIdGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Parts of this file based on:

// https://github.com/dotnet/runtime/blob/e52462326be03fb329384b7e04a33d3eb7c16736/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/RandomNumberGenerator.cs
// https://github.com/dotnet/runtime/blob/e52462326be03fb329384b7e04a33d3eb7c16736/src/libraries/System.Private.CoreLib/src/System/Random.Xoshiro256StarStarImpl.cs
// https://prng.di.unimi.it/xoshiro256starstar.c

// Xoshiro256**
//
//   Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
//
//   To the extent possible under law, the author has dedicated all copyright
//   and related and neighboring rights to this software to the public domain
//   worldwide. This software is distributed without any warranty.
//
//   See <http://creativecommons.org/publicdomain/zero/1.0/>.

// for the .NET 6+ implementation, see RandomIdGenerator.Net6.cs

#if !NET6_0_OR_GREATER

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.DataStreamsMonitoring.Utils;

namespace Datadog.Trace.Util;

/// <summary>
/// Generates random numbers suitable for use as Datadog trace ids and span ids.
/// </summary>
internal sealed class RandomIdGenerator : IRandomIdGenerator
{
    [ThreadStatic]
    private static RandomIdGenerator? _shared;

#if !NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Buffer used to avoid allocating a new byte array each time we generate a 128-bit trace id.
    /// </summary>
    private static byte[]? _buffer;
#endif

    // in .NET < 6, we implement Xoshiro256** instead of using System.Random,
    // so we need to keep some state. it is not safe to access from multiple threads,
    // hence the threadstatic field.
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public RandomIdGenerator()
    {
#if NETCOREAPP
        // don't allocate this inside the loop (CA2014)
        Span<Guid> guidSpan = stackalloc Guid[2];
#endif

        do
        {
            // generate two guids as a source of random bytes for the initial PRNG state.
            // reinterpret the 32 bytes (16 bytes x 2) as Int64s (8 bytes x 4).

#if NETCOREAPP
            guidSpan[0] = Guid.NewGuid();
            guidSpan[1] = Guid.NewGuid();

            var int64Span = System.Runtime.InteropServices.MemoryMarshal.Cast<Guid, ulong>(guidSpan);

            _s0 = int64Span[0];
            _s1 = int64Span[1];
            _s2 = int64Span[2];
            _s3 = int64Span[3];
#else
            // we can't use `unsafe` pointers in this code because it can be called
            // from manual instrumentation which could be running in partial trust.
            // if we ever drop support for partial trust,
            // we can rewrite this to use `unsafe` instead of allocating these arrays.
            var guidBytes1 = Guid.NewGuid().ToByteArray();
            var guidBytes2 = Guid.NewGuid().ToByteArray();

            _s0 = BitConverter.ToUInt64(guidBytes1, startIndex: 0);
            _s1 = BitConverter.ToUInt64(guidBytes1, startIndex: 8);
            _s2 = BitConverter.ToUInt64(guidBytes2, startIndex: 0);
            _s3 = BitConverter.ToUInt64(guidBytes2, startIndex: 8);
#endif

            // Guid uses the 4 most significant bits of the first long as the version which would be fixed and not randomized.
            // and uses 2 other bits in the second long for variants which would be fixed and not randomized too.
            // let's overwrite the fixed bits in each long part by the other long.
            _s0 = (_s0 & 0x0FFFFFFFFFFFFFFF) | (_s1 & 0xF000000000000000);
            _s2 = (_s2 & 0x0FFFFFFFFFFFFFFF) | (_s3 & 0xF000000000000000);
            _s1 = (_s1 & 0xFFFFFFFFFFFFFF3F) | (_s0 & 0x00000000000000C0);
            _s3 = (_s3 & 0xFFFFFFFFFFFFFF3F) | (_s2 & 0x00000000000000C0);
        }
        while ((_s0 | _s1 | _s2 | _s3) == 0);
    }

    public static RandomIdGenerator Shared => _shared ??= new RandomIdGenerator();

#if !NETCOREAPP3_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GetBuffer(int size)
    {
        if (_buffer == null || _buffer.Length < size)
        {
            _buffer = new byte[size];
        }

        return _buffer;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

    /// <summary>
    /// Produces a value in the range [0, ulong.MaxValue].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong NextUInt64()
    {
        var s0 = _s0;
        var s1 = _s1;
        var s2 = _s2;
        var s3 = _s3;

        var result = RotateLeft(s1 * 5, 7) * 9;
        var t = s1 << 17;

        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;

        s2 ^= t;
        s3 = RotateLeft(s3, 45);

        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;

        return result;
    }

    /// <summary>
    /// Returns a random number that is greater than zero
    /// and less than or equal to Int64.MaxValue (0x7fffffffffffffff).
    /// Used for backwards compatibility with tracers that parse ids as signed integers.
    /// </summary>
    private ulong NextLegacyId()
    {
        while (true)
        {
            // get a value in the range [0, UInt64.MaxValue]
            var result = NextUInt64();

            // shift bits right to get a value in the range [0, Int64.MaxValue]
            result >>= 1;

            if (result > 0)
            {
                // try again if the value is 0
                return result;
            }
        }
    }

    /// <inheritDoc />
    public ulong NextSpanId(bool useAllBits = false)
    {
        if (!useAllBits)
        {
            // get a value in the range (0, Int64.MaxValue]
            return NextLegacyId();
        }

        while (true)
        {
            // get a value in the range [0, UInt64.MaxValue]
            var result = NextUInt64();

            if (result > 0)
            {
                // try again if the value is 0
                return result;
            }
        }
    }

    /// <summary>
    /// Returns a random 128-bit number that is greater than zero
    /// and less than or equal to Int128.MaxValue (0xffffffffffffffffffffffffffffffff).
    /// </summary>
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

        while (true)
        {
            var lower = NextUInt64();

            if (lower > 0)
            {
                return new TraceId(upper, lower);
            }
        }
    }
}

#endif // !NET6_0_OR_GREATER
