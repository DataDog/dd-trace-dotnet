// <copyright file="FibonacciComputation.cs" company="Datadog">
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
    public class FibonacciComputation
    {
        private readonly int _value;
        private readonly int _nbThreads;
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public FibonacciComputation(int nbThreads)
            : this(32, nbThreads)
        {
        }

        public FibonacciComputation(int n, int nbThreads)
        {
            _value = n;
            _nbThreads = nbThreads;
        }

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);
            _activeTasks = CreateThreads();
        }

        public void Run()
        {
            _stopEvent = new ManualResetEvent(false);

            DoFibonacciComputation();

            _stopEvent.Dispose();
            _stopEvent = null;
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

        public long DoFibonacciComputation()
        {
            Console.WriteLine($"Starting {nameof(DoFibonacciComputation)}.");

            var start = Stopwatch.StartNew();
            long result = 0;
            try
            {
                result = DoComputationImpl(_value);
            }
            finally
            {
                Console.WriteLine($"  Fibonacci: It took {start.Elapsed.TotalSeconds} s, depth: {_value}. Result: {result}");
                Console.WriteLine($"Exiting {nameof(DoFibonacciComputation)}.");
                Console.WriteLine();
            }

            return result;
        }

        public long DoComputationImpl(int number)
        {
            if (IsEventSet())
            {
                return long.MinValue; // do not care about the value
            }

            if (number == 0)
            {
                return 0;
            }

            if (number == 1)
            {
                return 1;
            }

            return DoComputationImpl(number - 1) + DoComputationImpl(number - 2);
        }

        private List<Task> CreateThreads()
        {
            var result = new List<Task>(_nbThreads);

            for (var i = 0; i < _nbThreads; i++)
            {
                result.Add(
                    Task.Factory.StartNew(
                        () =>
                        {
                            while (!IsEventSet())
                            {
                                DoFibonacciComputation();
                            }
                        },
                        TaskCreationOptions.LongRunning));
            }

            return result;
        }

        private bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }
    }
}
