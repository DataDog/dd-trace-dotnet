// <copyright file="LinuxDlIteratePhdrDeadlock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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

        public LinuxDlIteratePhdrDeadlock()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
        }

        private static string ApiWrapperPath { get; } = Environment.GetEnvironmentVariable("LD_PRELOAD");

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

        private static IntPtr DllImportResolver(string resolverName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (resolverName == "DatadogApiWrapper")
            {
                return NativeLibrary.Load(ApiWrapperPath, assembly, searchPath);
            }

            return IntPtr.Zero;
        }

        [DllImport("DatadogApiWrapper", EntryPoint = "dlopen")]
        private static extern IntPtr Dlopen(string filename, int flags);

        [DllImport("DatadogApiWrapper", EntryPoint = "dlclose")]
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
#endif
