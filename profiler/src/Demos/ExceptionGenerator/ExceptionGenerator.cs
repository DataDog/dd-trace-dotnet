// <copyright file="ExceptionGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Demos.ExceptionGenerator
{
    internal class ExceptionGenerator
    {
        private const int MaxWorkRecursionDepth = 4;
        private const bool PrintExceptionsToConsole = false;

        private static readonly TimeSpan StatsPeriodDuration = TimeSpan.FromSeconds(5);
        private readonly Thread _thread;
        private volatile bool _isStopped;

        public ExceptionGenerator()
        {
            _thread = new Thread(MainLoop)
            {
                Name = "Exception Generator Thread"
            };
            _isStopped = false;
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Stop()
        {
            _isStopped = true;
            _thread.Join();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PerformWork01(int workRecursionDepth)
        {
            PerformWork02(workRecursionDepth);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PerformWork02(int workRecursionDepth)
        {
            PerformWork03(workRecursionDepth);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PerformWork03(int workRecursionDepth)
        {
            PerformWork04(workRecursionDepth);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PerformWork04(int workRecursionDepth)
        {
            PerformWork05(workRecursionDepth);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PerformWork05(int workRecursionDepth)
        {
            if (workRecursionDepth < MaxWorkRecursionDepth)
            {
                PerformWork01(workRecursionDepth + 1);
            }
            else
            {
                throw new Exception("Test Exception");
            }
        }

        private static int CountChars(string s, char c)
        {
            if (s == null)
            {
                return 0;
            }

            int count = 0;
            for (int p = 0; p < s.Length; p++)
            {
                if (s[p] == c)
                {
                    count++;
                }
            }

            return count;
        }

        private void MainLoop()
        {
            DateTimeOffset statsPeriodStartTime, startTime;
            statsPeriodStartTime = startTime = DateTimeOffset.Now;

            int totalInvocations = 0;
            int statsPeriodInvocations = 0;

            int totalExceptions = 0;
            int statsPeriodExceptions = 0;

            while (!_isStopped)
            {
                try
                {
                    PerformWork01(1);
                }
                catch (Exception ex)
                {
                    totalExceptions++;
                    statsPeriodExceptions++;

                    int stackLinesCount = CountChars(ex.StackTrace, '\n') + 1;  // last line has no '\n'.

#pragma warning disable CS0162  // Unreachable code detected (intentional const bool compile-time config)
                    if (PrintExceptionsToConsole)
                    {
                        Console.WriteLine($"\nException detected. Stack depth: {stackLinesCount}. Details:\n{ex.ToString()}");
                    }
#pragma warning restore CS0162 // Unreachable code detected
                }

                DateTimeOffset invokeEnd = DateTimeOffset.Now;

                statsPeriodInvocations++;
                totalInvocations++;

                TimeSpan statsPeriodRuntime = invokeEnd - statsPeriodStartTime;
                if (statsPeriodRuntime > StatsPeriodDuration)
                {
                    Console.WriteLine();
                    Console.WriteLine("Latest stats period:");
                    Console.WriteLine($"  Invocations:             {statsPeriodInvocations}.");
                    Console.WriteLine($"  Exceptions:              {statsPeriodExceptions}.");
                    Console.WriteLine($"  Time:                    {statsPeriodRuntime}.");
                    Console.WriteLine($"  Mean invocations/sec:    {statsPeriodInvocations / (statsPeriodRuntime).TotalSeconds}.");

                    TimeSpan totalRuntime = invokeEnd - startTime;
                    Console.WriteLine("Total:");
                    Console.WriteLine($"  Invocations:             {totalInvocations}.");
                    Console.WriteLine($"  Exceptions:              {totalExceptions}.");
                    Console.WriteLine($"  Time:                    {totalRuntime}.");
                    Console.WriteLine($"  Mean invocations/sec:    {totalInvocations / (totalRuntime).TotalSeconds}.");

                    statsPeriodInvocations = 0;
                    statsPeriodExceptions = 0;
                    statsPeriodStartTime = invokeEnd;
                }

                Thread.Yield();
            }
        }
    }
}
