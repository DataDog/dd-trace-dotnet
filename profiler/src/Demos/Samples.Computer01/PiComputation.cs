// <copyright file="PiComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class PiComputation
    {
        private CancellationTokenSource _cancellationTokenSource;
        private List<Thread> _activeTasks;

        public void Start()
        {
            if (_cancellationTokenSource != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _activeTasks = new List<Thread>();

            var t = new Thread(() =>
            {
                if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
                {
                    Thread.CurrentThread.Name = "PiComputation-" + Thread.CurrentThread.ManagedThreadId;
                }

                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    DoPiComputation();
                }
            });

            t.Start();

            _activeTasks.Add(t);
        }

        public void Stop()
        {
            if (_cancellationTokenSource == null)
            {
                throw new InvalidOperationException("Not running...");
            }

            _cancellationTokenSource.Cancel();

            foreach (var thread in _activeTasks)
            {
                thread.Join();
            }

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _activeTasks = null;
        }

        public void Run()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            DoPiComputation();

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        private void DoPiComputation()
        {
            // ~ 7 seconds on a P70 laptop

            ulong denominator = 1;
            int numerator = 1;
            double pi = 1;
            var sw = new Stopwatch();
            sw.Start();

            Console.WriteLine($"Starting {nameof(DoPiComputation)}.");

            int currentIteration = 0;
            while (
                sw.Elapsed.TotalSeconds < 7 &&
                !_cancellationTokenSource.IsCancellationRequested)
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
