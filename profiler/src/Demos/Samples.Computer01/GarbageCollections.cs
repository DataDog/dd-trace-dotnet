// <copyright file="GarbageCollections.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class GarbageCollections
    {
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        private int _generation;

        public GarbageCollections(int generation)
        {
            _generation = generation;
        }

        public void TriggerCollections()
        {
            GC.Collect(_generation, GCCollectionMode.Forced);
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
            TriggerCollections();
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
            var result = new List<Task>();

            result.Add(
                Task.Factory.StartNew(
                    () =>
                    {
                        while (!IsEventSet())
                        {
                            TriggerCollections();
                        }
                    },
                    TaskCreationOptions.LongRunning));

            return result;
        }

        private bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }
    }
}
