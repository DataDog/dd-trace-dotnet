// <copyright file="ParallelExceptionsScenario.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Datadog.Demos.ExceptionGenerator
{
    public class ParallelExceptionsScenario
    {
        private const int NumberOfThreads = 4;
        private const int ExceptionsPerThread = 1000;

        public void Run()
        {
            var barrier = new Barrier(NumberOfThreads);

            // Use threads instead of tasks to have a predictable stacktrace
            var threads = new Thread[NumberOfThreads];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(ThrowExceptions);
                threads[i].Start(barrier);
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        private void ThrowExceptions(object state)
        {
            var barrier = (Barrier)state;
            barrier.SignalAndWait();

            for (int i = 0; i < ExceptionsPerThread; i++)
            {
                try
                {
                    throw new Exception();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
