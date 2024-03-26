// <copyright file="ThreadSpikes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class ThreadSpikes : ScenarioBase
    {
        private readonly int _waitDuration;
        private readonly int _threadCount;

        public ThreadSpikes(int threadCount, int duration)
        {
            _waitDuration = duration;
            _threadCount = threadCount;
        }

        public override void OnProcess()
        {
            Console.WriteLine($"|> count = {System.Diagnostics.Process.GetCurrentProcess().Threads.Count}");
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < _threadCount; i++)
            {
                tasks.Add(Task.Factory.StartNew(
                    () =>
                    {
                        var current = i;
                        Thread.Sleep(_waitDuration);
                    },
                    TaskCreationOptions.LongRunning));
            }

            Thread.Sleep(50);
            Console.WriteLine($"<| count = {System.Diagnostics.Process.GetCurrentProcess().Threads.Count}");
            Task.WhenAll(tasks).Wait();
            Thread.Sleep(_waitDuration);
            Console.WriteLine("--------------------------\r\n");
        }
    }
}
