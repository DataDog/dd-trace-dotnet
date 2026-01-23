// <copyright file="ManyClosures.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.ClosureOverflowSamples;

internal static class ManyClosures
{
    internal const int ClosureCount = 64;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Run()
    {
        var baseValue = 123;

        // The captured variable forces the compiler to emit a single display class (nested compiler-generated type)
        // with many generated methods. This previously overflowed the initial closure buffer heuristic.
        Func<int>[] funcs = new Func<int>[]
        {
            () => baseValue + 0,
            () => baseValue + 1,
            () => baseValue + 2,
            () => baseValue + 3,
            () => baseValue + 4,
            () => baseValue + 5,
            () => baseValue + 6,
            () => baseValue + 7,
            () => baseValue + 8,
            () => baseValue + 9,
            () => baseValue + 10,
            () => baseValue + 11,
            () => baseValue + 12,
            () => baseValue + 13,
            () => baseValue + 14,
            () => baseValue + 15,
            () => baseValue + 16,
            () => baseValue + 17,
            () => baseValue + 18,
            () => baseValue + 19,
            () => baseValue + 20,
            () => baseValue + 21,
            () => baseValue + 22,
            () => baseValue + 23,
            () => baseValue + 24,
            () => baseValue + 25,
            () => baseValue + 26,
            () => baseValue + 27,
            () => baseValue + 28,
            () => baseValue + 29,
            () => baseValue + 30,
            () => baseValue + 31,
            () => baseValue + 32,
            () => baseValue + 33,
            () => baseValue + 34,
            () => baseValue + 35,
            () => baseValue + 36,
            () => baseValue + 37,
            () => baseValue + 38,
            () => baseValue + 39,
            () => baseValue + 40,
            () => baseValue + 41,
            () => baseValue + 42,
            () => baseValue + 43,
            () => baseValue + 44,
            () => baseValue + 45,
            () => baseValue + 46,
            () => baseValue + 47,
            () => baseValue + 48,
            () => baseValue + 49,
            () => baseValue + 50,
            () => baseValue + 51,
            () => baseValue + 52,
            () => baseValue + 53,
            () => baseValue + 54,
            () => baseValue + 55,
            () => baseValue + 56,
            () => baseValue + 57,
            () => baseValue + 58,
            () => baseValue + 59,
            () => baseValue + 60,
            () => baseValue + 61,
            () => baseValue + 62,
            () => baseValue + 63,
        };

        // Prevent the JIT from treating the array as unused and dropping codepaths in some configurations.
        var sum = 0;
        for (int i = 0; i < funcs.Length; i++)
        {
            sum += funcs[i]();
        }

        return sum;
    }
}
