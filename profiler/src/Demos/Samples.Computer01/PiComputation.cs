// <copyright file="PiComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;

namespace Samples.Computer01
{
    public class PiComputation : ScenarioBase
    {
        public override void OnProcess()
        {
            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "PiComputation-" + Thread.CurrentThread.ManagedThreadId;
            }

            DoPiComputation();
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
                !IsEventSet())
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
