// <copyright file="IteratorComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class IteratorComputation
    {
        private readonly int _nbThreads;
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public IteratorComputation(int nbThreads)
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
            CallIterators();
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
                                CallIterators();
                            }
                        },
                        TaskCreationOptions.LongRunning));
            }

            return result;
        }

        private void CallIterators()
        {
            Iterator it = new Iterator(1000);
        }

        private bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }

        // this is used to see the name of the constructor method (i.e. .ctor)
        public class Iterator
        {
            public Iterator(int count)
            {
                var sequence = GetEvenSequence(count);
                Console.WriteLine($"sequence has {sequence.Count()} elements");
            }

            private static IEnumerable<int> GetEvenSequence(int count)
            {
                int i = 0;
                while (true)
                {
                    if (i % 2 == 0)
                    {
                        Thread.Sleep(10);
                        yield return 2;
                    }

                    i++;

                    if (--count <= 0)
                    {
                        yield break;
                    }
                }
            }
        }
    }
}
