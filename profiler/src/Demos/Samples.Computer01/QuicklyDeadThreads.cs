// <copyright file="QuicklyDeadThreads.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class QuicklyDeadThreads
    {
        private const int SleepDurationMs = 0;
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        private const int MaxCreatedThreadCount = 1024;

        public QuicklyDeadThreads()
        {
        }

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
                        if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
                        {
                            Thread.CurrentThread.Name = "QuickDeadThreads";
                        }

                        while (!_stopEvent.WaitOne(SleepDurationMs))
                        {
                            CreateQuicklyDeadThreads();
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

            CreateQuicklyDeadThreads();

            _stopEvent.Dispose();
            _stopEvent = null;
        }

        private void CreateQuicklyDeadThreads()
        {
            Console.WriteLine($"Starting {nameof(CreateQuicklyDeadThreads)}.");

            int count = 0;
            while (!_stopEvent.WaitOne(SleepDurationMs) && (count < MaxCreatedThreadCount))
            {
                Thread t = new Thread(() => { count++; });
                t.Start();
                t.Join();
            }

            Console.WriteLine($"   {count} threads were created.");
        }
    }
}
