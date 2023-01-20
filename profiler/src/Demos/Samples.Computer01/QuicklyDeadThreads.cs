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
        private readonly int _nbThreadsToCreate = 1024;
        private readonly int _nbThreads = 1;

        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public QuicklyDeadThreads(int nbThreads, int nbThreadsToCreate)
        {
            _nbThreads = nbThreads;
            _nbThreadsToCreate = nbThreadsToCreate;
        }

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);
            _activeTasks = CreateTasks();
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
            Start();
            Task.WhenAll(_activeTasks).Wait();
        }

        private List<Task> CreateTasks()
        {
            var tasks = new List<Task>(_nbThreads);
            for (var i = 0; i < _nbThreads; i++)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        (p) =>
                        {
                            Console.WriteLine($"Starting generator-{p}");
                            int count = 0;
                            while (!_stopEvent.WaitOne(SleepDurationMs) && (count < _nbThreadsToCreate))
                            {
                                Thread t = new Thread(() => { count++; });
                                t.IsBackground = true;
                                t.Start();
                                t.Join();
                            }

                            Console.WriteLine($"   {count} threads by generator-{p}");

                            // ensure threads are collected
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                        },
                        i));
            }

            return tasks;
        }
    }
}
