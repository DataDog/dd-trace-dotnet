// <copyright file="GenericsAllocation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "for tests")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "for tests")]
    public class Generic<T>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "for tests")]
        public T _field;

        public Generic(T instance)
        {
            _field = instance;
        }
    }

    public class GenericsAllocation
    {
        // The array will always trigger an AllocationTick event since 100KB is the threshold
        // and a few elements will also trigger the event
        private const int BufferSize = (100 * 1024) + 1;

        private readonly int _nbThreads;
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public GenericsAllocation(int nbThreads)
        {
            _nbThreads = nbThreads;
        }

        public void AllocateGeneric()
        {
            var buffer = new Generic<int>[BufferSize];
            for (int i = 0; i < BufferSize; i++)
            {
                buffer[i] = new Generic<int>(i);
            }
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
            AllocateGeneric();
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
                                AllocateGeneric();
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
