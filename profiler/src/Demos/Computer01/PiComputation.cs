// <copyright file="PiComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Demos.Computer01
{
    public class PiComputation
    {
        private const int SleepDurationMs = 0;

        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);
            _activeTasks = new List<Task>
            {
                Task.Factory.StartNew(
                    () =>
                    {
                        while (!_stopEvent.WaitOne(SleepDurationMs))
                        {
                            DoPiComputation();
                        }
                    },
                    TaskCreationOptions.LongRunning),
            };
        }

        public void Stop()
        {
            if (_stopEvent == null)
            {
                throw new InvalidOperationException("Not running...");
            }

            _stopEvent.Set();

            Task.WhenAll(_activeTasks).Wait();

            _stopEvent.Dispose();
            _stopEvent = null;
            _activeTasks = null;
        }

        public void Run()
        {
            _stopEvent = new ManualResetEvent(false);

            DoPiComputation();

            _stopEvent.Dispose();
            _stopEvent = null;
        }

        private void DoPiComputation()
        {
            // ~ 7 seconds on a P70 laptop
            const int maxIteration = 20000000;
            ulong denominator = 1;
            int numerator = 1;
            double pi = 1;
            var sw = new Stopwatch();
            sw.Start();

            Console.WriteLine($"Starting {nameof(DoPiComputation)}.");

            int currentIteration = 0;
            while (
                (currentIteration < maxIteration) &&
                !_stopEvent.WaitOne(SleepDurationMs))
            {
                numerator = -numerator;
                denominator += 2;
                pi += ((double)numerator) / ((double)denominator);

                currentIteration++;
            }

            pi *= 4.0;
            sw.Stop();

            Console.WriteLine($"   pi ~ {pi} after {currentIteration} iterations in {sw.Elapsed}");
            Console.WriteLine($"Exiting {nameof(DoPiComputation)}.");
            Console.WriteLine();
        }
    }
}
