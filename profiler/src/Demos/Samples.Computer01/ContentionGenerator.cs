// <copyright file="ContentionGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Samples.Computer01
{
    internal class ContentionGenerator : ScenarioBase
    {
        private readonly int _lockDuration;
        private readonly object _obj = new object();

        public ContentionGenerator(int nbThreads, int lockDuration)
            : base(nbThreads)
        {
            _lockDuration = lockDuration;
        }

        public override void Run()
        {
            Start();
            Thread.Sleep(TimeSpan.FromSeconds(1)); // to be sure that we got contention events
            Stop();
        }

        public override void OnProcess()
        {
            GenerateContention();
        }

        private void GenerateContention()
        {
            lock (_obj)
            {
                Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} acquired the lock");
                Thread.Sleep(TimeSpan.FromMilliseconds(_lockDuration));
            }

            Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} released the lock");
        }
    }
}
