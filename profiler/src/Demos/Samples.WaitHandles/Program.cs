// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Samples.WaitHandles
{
    public class Program
    {
        static AutoResetEvent autoResetEvent = new AutoResetEvent(false);
        static ManualResetEvent manualResetEvent = new ManualResetEvent(false);
        static Mutex mutex = new Mutex();
        static Semaphore semaphore = new Semaphore(1, 1);

        public static void Main()
        {
            Console.WriteLine($"pid = {Process.GetCurrentProcess().Id}");
            Console.WriteLine("Press Enter to start the threads...");
            //Console.ReadLine();

            // Create and start threads for different synchronization scenarios
            Console.WriteLine();
            Console.WriteLine("Starting threads...");

            Stopwatch sw = new Stopwatch();
            for (int i = 0; i < 5; i++)
            {
                sw.Restart();

                // start a thread that will own the mutex and the semaphore
                var owningThread = new Thread(OwningThread);
                owningThread.Start();
                Thread.Sleep(5);

                var autoResetEventThread = new Thread(AutoResetEventThread);
                autoResetEventThread.Start();

                var manualResetEventThread = new Thread(ManualResetEventThread);
                manualResetEventThread.Start();

                var mutexThread = new Thread(MutexThread);
                mutexThread.Start();

                var semaphoreThread = new Thread(SemaphoreThread);
                semaphoreThread.Start();

                // wait for all threads to finish
                owningThread.Join();
                autoResetEventThread.Join();
                manualResetEventThread.Join();
                mutexThread.Join();
                semaphoreThread.Join();

                sw.Stop();
                Console.WriteLine("___________________________________________");
                Console.WriteLine($"Duration = {sw.ElapsedMilliseconds} ms");

                Thread.Sleep(2);
            }
        }

        private static void OwningThread()
        {
            Console.WriteLine();
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] Start to hold resources");
            Console.WriteLine("___________________________________________");
            mutex.WaitOne();
            semaphore.WaitOne();

            Thread.Sleep(3000);
            Console.WriteLine();
            Console.WriteLine("    Release resources");

            mutex.ReleaseMutex();
            semaphore.Release(1);
            autoResetEvent.Set();
            manualResetEvent.Set();
        }

        private static void AutoResetEventThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for AutoResetEvent...");
            autoResetEvent.WaitOne();
            Console.WriteLine("    <-- AutoResetEvent");
        }

        private static void ManualResetEventThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for ManualResetEvent...");
            manualResetEvent.WaitOne();
            manualResetEvent.Reset();
            Console.WriteLine("    <-- ManualResetEvent");
        }

        private static void MutexThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for Mutex...");
            mutex.WaitOne();
            mutex.ReleaseMutex();
            Console.WriteLine("    <-- Mutex");
        }

        private static void SemaphoreThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for Semaphore");
            semaphore.WaitOne();
            semaphore.Release(1);
            Console.WriteLine("    <-- Semaphore");
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }
}
