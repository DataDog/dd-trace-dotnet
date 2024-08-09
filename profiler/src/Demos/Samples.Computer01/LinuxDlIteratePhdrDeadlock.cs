// <copyright file="LinuxDlIteratePhdrDeadlock.cs" company="Datadog">
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
    internal class LinuxDlIteratePhdrDeadlock
    {
        private ManualResetEvent _stopEvent;
        private Task _exceptionTask;
        private Thread _worker;

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);

            _worker = new Thread(ExecuteCallToDlOpenDlClose)
            {
                IsBackground = false // set to false to prevent the app from shutting down. The test will fail
            };
            _worker.Start();

            _exceptionTask = Task.Factory.StartNew(
                        () =>
                        {
                            var nbException = 0;
                            var loggingClock = Stopwatch.StartNew();
                            while (!IsEventSet())
                            {
                                if (loggingClock.ElapsedMilliseconds >= 1000)
                                {
                                    Console.WriteLine($"* Nb thrown exception {nbException}");
                                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                                    nbException = 0;
                                    loggingClock.Restart();
                                }

                                for (var i = 0; i < 20; i++)
                                {
                                    try
                                    {
                                        throw new Exception("dl_iterate_phdr deadlock exception");
                                    }
                                    catch { }
                                    nbException++;
                                }

                                // wait a bit randomly (23 is a prime number chosen randomly)
                                Thread.Sleep(TimeSpan.FromMilliseconds(23));
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

            _worker.Join();
            _exceptionTask.Wait();

            _stopEvent.Dispose();
            _stopEvent = null;
        }

        [DllImport("libdl.so", EntryPoint = "dlopen")]
        private static extern IntPtr Dlopen(string filename, int flags);

        [DllImport("libdl.so", EntryPoint = "dlclose")]
        private static extern void DlClose(IntPtr handle);

        private void ExecuteCallToDlOpenDlClose()
        {
            var loggingClock = Stopwatch.StartNew();
            var counter = 0;

            while (!IsEventSet())
            {
                if (loggingClock.ElapsedMilliseconds >= 1000)
                {
                    Console.WriteLine($"* Nb execution {counter}");
                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                    counter = 0;
                    loggingClock.Restart();
                }

                var handle = Dlopen("libc.so.6", 2);
                Thread.Sleep(TimeSpan.FromMilliseconds(10));
                DlClose(handle);

                counter++;
            }
        }

        private bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }
    }
}
