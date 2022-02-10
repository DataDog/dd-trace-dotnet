// <copyright file="SleepManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Demos.Computer01
{
    public class SleepManager
    {
        private readonly int _value;
        private readonly int _nbThreads;
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public SleepManager(int nbThreads)
            : this(32, nbThreads)
        {
        }

        public SleepManager(int n, int nbThreads)
        {
            _value = n;
            _nbThreads = nbThreads;

            ThreadPool.SetMinThreads(nbThreads + 10, 32);
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

            DoSleep();

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

        public void DoSleep()
        {
            Console.WriteLine($"Starting {nameof(DoSleep)}.");

            _stopEvent.WaitOne(Timeout.InfiniteTimeSpan);

            Console.WriteLine($"Stopping {nameof(DoSleep)}.");
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
                                DoSleep();
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
