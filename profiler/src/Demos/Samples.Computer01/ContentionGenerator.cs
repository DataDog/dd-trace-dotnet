// <copyright file="ContentionGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    internal class ContentionGenerator
    {
        private readonly int _nbThreads;
        private readonly object _obj = new object();
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public ContentionGenerator(int nbThreads)
        {
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
            Start();
            Thread.Sleep(TimeSpan.FromSeconds(1)); // to be sure that we got contention events
            Stop();
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
                                GenerateContention();
                            }
                        },
                        TaskCreationOptions.LongRunning));
            }

            return result;
        }

        private void GenerateContention()
        {
            lock (_obj)
            {
                Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} acquired the lock");
                Thread.Sleep(TimeSpan.FromMilliseconds(300));
            }

            Console.WriteLine($"Thread {Environment.CurrentManagedThreadId} released the lock");
        }

        private bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }
    }
}
