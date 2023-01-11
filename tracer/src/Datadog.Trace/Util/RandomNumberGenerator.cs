// <copyright file="RandomNumberGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/RandomNumberGenerator.cs
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Random.Xoshiro256StarStarImpl.cs
// https://prng.di.unimi.it/xoshiro256starstar.c

#nullable enable

using System;

namespace Datadog.Trace.Util;

/// <summary>
/// RandomNumberGenerator implementation is the 64-bit random number generator
/// based on the Xoshiro256StarStar algorithm (known as shift-register generators).
/// </summary>
internal sealed class RandomNumberGenerator
{
    [ThreadStatic]
    private static RandomNumberGenerator? _random;

    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public unsafe RandomNumberGenerator()
    {
        do
        {
            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var g1p = (ulong*)&g1;
            var g2p = (ulong*)&g2;
            _s0 = *g1p;
            _s1 = *(g1p + 1);
            _s2 = *g2p;
            _s3 = *(g2p + 1);

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

    public static RandomNumberGenerator Current => _random ??= new RandomNumberGenerator();

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;

        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }
}
