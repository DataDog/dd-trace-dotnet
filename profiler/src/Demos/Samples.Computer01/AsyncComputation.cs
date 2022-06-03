// <copyright file="AsyncComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class AsyncComputation
    {
        private readonly int _nbThreads;
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public AsyncComputation(int nbThreads)
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
            Compute1().Wait();
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
                                Compute1().Wait();
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

        private async Task Compute1()
        {
            ConsumeCPU();
            await Compute2();
            ConsumeCPUAfterCompute2();
        }

        private async Task Compute2()
        {
            ConsumeCPU();
            await Compute3();
            ConsumeCPUAfterCompute3();
        }

        private async Task Compute3()
        {
            await Task.Delay(1000);
            ConsumeCPUinCompute3();
            Console.WriteLine("Exit Compute3");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConsumeCPUinCompute3()
        {
            ConsumeCPU();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConsumeCPUAfterCompute2()
        {
            ConsumeCPU();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConsumeCPUAfterCompute3()
        {
            ConsumeCPU();
        }

        private void ConsumeCPU()
        {
            for (int i = 0; i < 1000; i++)
            {
                for (int j = 0; j < 1000000; j++)
                {
                    Math.Sqrt((double)j);
                }
            }
        }
    }
}
