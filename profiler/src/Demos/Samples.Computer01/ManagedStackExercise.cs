// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

using System;
using System.Runtime.CompilerServices;

namespace Samples.Computer01
{
    internal static class ManagedStackExercise
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Run()
        {
            RecursiveCallChain(6, Environment.TickCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RecursiveCallChain(int depth, int seed)
        {
            if (depth == 0)
            {
                return seed ^ 0x5A5A;
            }

            var next = RecursiveCallChain(depth - 1, seed + depth);
            var result = (next * 31) ^ depth;
            return result + seed;
        }
    }
}

