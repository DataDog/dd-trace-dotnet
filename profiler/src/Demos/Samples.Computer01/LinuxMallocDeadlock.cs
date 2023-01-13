// <copyright file="LinuxMallocDeadlock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    internal class LinuxMallocDeadLock
    {
        private ManualResetEvent _stopEvent;
        private Task _runningTask;

        public LinuxMallocDeadLock()
        {
        }

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);

            var counter = 0;
            _runningTask = Task.Factory.StartNew(
                        () =>
                        {
                            var loggingClock = Stopwatch.StartNew();
                            while (!IsEventSet())
                            {
                                if (loggingClock.ElapsedMilliseconds >= 1000)
                                {
                                    Console.WriteLine($"* Nb execution {counter}");
                                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                                    counter = 0;
                                    loggingClock.Restart();
                                }

                                var thread = new Thread(ExecuteAllocationScenario)
                                {
                                    IsBackground = false // set to false to prevent the app from shutting down. The test will fail
                                };
                                thread.Start();
                                counter++;
                            }
                        },
                        TaskCreationOptions.LongRunning);
        }

        public void Run()
        {
            Start();
            Thread.Sleep(TimeSpan.FromSeconds(10));
            Stop();
        }

        public void Stop()
        {
            if (_stopEvent == null)
            {
                throw new InvalidOperationException("Not running...");
            }

            _stopEvent.Set();

            _runningTask.Wait();

            _stopEvent.Dispose();
            _stopEvent = null;
            _runningTask = null;
        }

        [DllImport("libc.so.6", EntryPoint = "malloc")]
        private static extern IntPtr Malloc(int size);

        [DllImport("libc.so.6", EntryPoint = "free")]
        private static extern void Free(IntPtr ptr);

        private static void ExecuteAllocationScenario()
        {
            var ptr = Malloc(1040);
            Thread.Sleep(TimeSpan.FromMilliseconds(10));
            Free(ptr);
        }

        private bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }
    }
}
