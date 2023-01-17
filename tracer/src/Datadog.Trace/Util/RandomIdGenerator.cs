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

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

/// <summary>
/// Generates random numbers suitable for use as Datadog trace ids and span ids.
/// </summary>
internal sealed class RandomIdGenerator
{
    [ThreadStatic]
    private static RandomIdGenerator? _shared;

    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public unsafe RandomIdGenerator()
    {
        do
        {
            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();

            var gp1 = (ulong*)&g1;
            var gp2 = (ulong*)&g2;

            _s0 = *gp1;
            _s1 = *(gp1 + 1);
            _s2 = *gp2;
            _s3 = *(gp2 + 1);

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
    /// Returns a random number that is greater than zero and less than or equal to Int64.MaxValue.
    /// </summary>
    public ulong NextSpanId()
    {
        while (true)
        {
            // Get top 63 bits to get a value in the range [0, Int64.MaxValue], but try again
            // if the value is 0 to get a value in the range (0, Int64.MaxValue].
            var result = NextUInt64() >> 1;

            if (result > 0)
            {
                return result;
            }
        }
    }
}
