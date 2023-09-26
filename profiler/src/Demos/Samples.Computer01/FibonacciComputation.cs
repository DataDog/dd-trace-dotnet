// <copyright file="FibonacciComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;

namespace Samples.Computer01
{
    public class FibonacciComputation : ScenarioBase
    {
        private readonly int _value;

        public FibonacciComputation(int nbThreads)
            : this(42, nbThreads)
        {
        }

        public FibonacciComputation(int n, int nbThreads)
            : base(nbThreads)
        {
            _value = n;
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

        public override void OnProcess()
        {
            DoFibonacciComputation();
        }
    }
}
