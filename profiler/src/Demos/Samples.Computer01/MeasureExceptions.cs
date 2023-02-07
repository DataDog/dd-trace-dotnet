// <copyright file="MeasureExceptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable CS0169 // Remove unused private members
#pragma warning disable SA1401 // Fields should be private

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    // IDEA: how to measure the impact of exceptions sampling on real count
    //  - have classes with different names
    //  - throw the same count of exceptions
    //  - throw exceptions in different orders

    internal class MeasureExceptions
    {
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public MeasureExceptions()
        {
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
            ThrowExceptions();
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

        private void ThrowExceptions()
        {
            // throw random exceptions
            List<ExceptionStats> exceptions = Initialize();
            var maxExceptionIndex = exceptions.Count;
            Random r = new Random(DateTime.Now.Millisecond);

            for (int i = 0; i < 20_000; i++)
            {
                int index = r.Next(maxExceptionIndex);
                exceptions[index].Count++;
                exceptions[index].Thrower();
            }

            DumpExceptions(exceptions);
        }

        private List<ExceptionStats> Initialize()
        {
            List<ExceptionStats> exceptions = new List<ExceptionStats>()
            {
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(ArgumentException),
                    Thrower = ThrowArgument
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(SystemException),
                    Thrower = ThrowSystem
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(InvalidOperationException),
                    Thrower = ThrowInvalidOperation
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(InvalidCastException),
                    Thrower = ThrowInvalidCast
                },
                new ExceptionStats()
                {
                    Count = 0,
                    Name = nameof(TimeoutException),
                    Thrower = ThrowTimeout
                },
            };

            return exceptions;
        }

        private void ThrowArgument()
        {
            try { throw new ArgumentException(); } catch { }
        }

        private void ThrowSystem()
        {
            try { throw new SystemException(); } catch { }
        }

        private void ThrowInvalidOperation()
        {
            try { throw new InvalidOperationException(); } catch { }
        }

        private void ThrowInvalidCast()
        {
            try { throw new InvalidCastException(); } catch { }
        }

        private void ThrowTimeout()
        {
            try { throw new TimeoutException(); } catch { }
        }

        private void DumpExceptions(IEnumerable<ExceptionStats> exceptions)
        {
            Console.WriteLine("Exceptions start");
            foreach (var exception in exceptions)
            {
                Console.WriteLine($"{exception.Name}={exception.Count}");
            }

            Console.WriteLine("Exceptions end");
            Console.WriteLine();
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
                            ThrowExceptions();
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

        internal class ExceptionStats
        {
            public string Name;

            public int Count;

            public Action Thrower;
        }
    }
}
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore CS0169 // Remove unused private members
