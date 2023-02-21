// <copyright file="SleepManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Samples.Computer01
{
    public class SleepManager : ScenarioBase
    {
        private readonly int _value;

        public SleepManager(int nbThreads)
            : this(32, nbThreads)
        {
        }

        public SleepManager(int n, int nbThreads)
            : base(nbThreads)
        {
            _value = n;

            ThreadPool.SetMinThreads(nbThreads + 10, 32);
        }

        public override void Run()
        {
            throw new InvalidOperationException("Run is unsupported for this scenario: it would lead to an infinite wait.");
        }

        public void DoSleep()
        {
            Console.WriteLine($"Starting {nameof(DoSleep)}.");

            WaitFor(Timeout.InfiniteTimeSpan);

            Console.WriteLine($"Stopping {nameof(DoSleep)}.");
        }

        public override void OnProcess()
        {
            DoSleep();
        }
    }
}
