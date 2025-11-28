// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Samples.Computer01
{
    internal static class ManagedStackExercise
    {
        private static volatile bool _shouldStop;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Run()
        {
            Console.WriteLine("ManagedStackExercise: Starting continuous execution...");
            _shouldStop = false;
            // Run continuously until timeout
            var sw = Stopwatch.StartNew();
            long iterations = 0;
            
            while (!_shouldStop)
            {
                Level1();
                iterations++;
            }
            
            Console.WriteLine($"ManagedStackExercise: Completed {iterations} iterations in {sw.Elapsed}");
        }

        public static void Stop()
        {
            _shouldStop = true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Level1()
        {
            Level2();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Level2()
        {
            Level3();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Level3()
        {
            Level4();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Level4()
        {
            Level5();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Level5()
        {
            // Do some work to consume CPU and make this worth profiling
            DoWork();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int DoWork()
        {
            // Simple computation to consume CPU
            int result = 0;
            for (int i = 0; i < 1000; i++)
            {
                result ^= i * 31;
            }
            return result;
        }
    }
}

