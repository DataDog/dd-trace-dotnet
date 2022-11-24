// <copyright file="MemoryLeak.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class MemoryLeak
    {
        // The array will always trigger an AllocationTick event since 100KB is the threshold
        // and a few elements will also trigger the event
        private const int BufferSize = (100 * 1024) + 1;

        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        private int _objectsToAllocateCount;

        public MemoryLeak(int parameter)
        {
            _objectsToAllocateCount = parameter;
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
            AllocateWithLeak();
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

        private void AllocateWithLeak()
        {
            List<byte[]> root = new List<byte[]>();
            int count = 0;

            while (!IsEventSet() && (count <= _objectsToAllocateCount))
            {
                root.Add(new byte[BufferSize]);
                GC.Collect();

                count++;
            }
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
                            AllocateWithLeak();
                        }
                    },
                    TaskCreationOptions.LongRunning));

            return result;
        }

        private bool IsEventSet()
        {
            if (_stopEvent == null)
            {
                return false;
            }

            return _stopEvent.WaitOne(0);
        }
    }
}
