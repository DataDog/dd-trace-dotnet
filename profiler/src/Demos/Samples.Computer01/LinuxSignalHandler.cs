// <copyright file="LinuxSignalHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if Linux && NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    internal class LinuxSignalHandler
    {
        private const PosixSignal SIGUSR1 = (PosixSignal)10;
        private static int _nbHandlerExecutions = 0;
        private static int _nbSignalsSent = 0;

        private ManualResetEvent _stopEvent;
        private Task _runningTask;
        private PosixSignalRegistration _currentPosixSignalRegistration;

        public LinuxSignalHandler()
        {
        }

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);

            _runningTask = Task.Factory.StartNew(
                        () =>
                        {
                            SetupSignalHandler();
                            var loggingClock = Stopwatch.StartNew();
                            while (!IsEventSet())
                            {
                                if (loggingClock.ElapsedMilliseconds >= 1000)
                                {
                                    LogStats();
                                    loggingClock.Restart();
                                }

                                SendSignal();
                                Thread.Sleep(TimeSpan.FromMilliseconds(19));
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
            _currentPosixSignalRegistration.Dispose();

            _currentPosixSignalRegistration = null;
            _stopEvent = null;
            _runningTask = null;
        }

        private static void SendSignal()
        {
            var result = Process.GetCurrentProcess().Kill(10);
            if (result != 0)
            {
                return;
            }

            _nbSignalsSent++;
        }

        private static void SignalHandler(PosixSignalContext ctx)
        {
            Interlocked.Increment(ref _nbHandlerExecutions);
        }

        private void SetupSignalHandler()
        {
            Console.WriteLine($"Installing signal handler for signal {SIGUSR1.ToString()}");
            _currentPosixSignalRegistration = PosixSignalRegistration.Create(SIGUSR1, SignalHandler);
        }

        private bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }

        private void LogStats()
        {
            Console.WriteLine("Signal scenario stats:");

            // Use "[Error] XX" pattern to fail integration tests
            if (_nbSignalsSent == 0)
            {
                Console.WriteLine("[Error] no signal was sent for the last period of time.");
            }
            else
            {
                Console.WriteLine($"* Number of signal {SIGUSR1} sent : {_nbSignalsSent}");
            }

            if (_nbHandlerExecutions == 0)
            {
                Console.WriteLine("[Error] signal handler was not executed for the last period of time.");
            }
            else
            {
                Console.WriteLine($"* Number of signal handler execution : {_nbHandlerExecutions}");
            }

            Console.WriteLine();

            _nbHandlerExecutions = 0;
            _nbSignalsSent = 0;
        }
    }
}

#endif
