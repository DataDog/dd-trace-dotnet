// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Samples.WaitHandles
{
    public class Program
    {
        private static AutoResetEvent autoResetEvent = new AutoResetEvent(false);
        private static ManualResetEvent manualResetEvent = new ManualResetEvent(false);
        private static ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false, 20);  // default is 10
        private static Mutex mutex = new Mutex();
        private static Semaphore semaphore = new Semaphore(1, 1);
        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        private static ReaderWriterLock rwlock = new ReaderWriterLock();
        private static ReaderWriterLockSlim rwlockSlim = new ReaderWriterLockSlim();

        public static void Main(string[] args)
        {
            // to support integration test, we need to support --iterations
            ParseCommandLine(args, out int iterations);

            Console.WriteLine($"pid = {Process.GetCurrentProcess().Id}");
            Console.WriteLine("Press Enter to start the threads...");

            // uncomment the following to allow attaching a tool
            // Console.ReadLine();

            // Create and start threads for different synchronization scenarios
            Console.WriteLine();
            Console.WriteLine("Starting threads...");

            Stopwatch sw = new Stopwatch();
            for (int i = 0; i < iterations; i++)
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

                var manualResetEventSlimThread = new Thread(ManualResetEventSlimThread);
                manualResetEventSlimThread.Start();

                var mutexThread = new Thread(MutexThread);
                mutexThread.Start();

                var semaphoreThread = new Thread(SemaphoreThread);
                semaphoreThread.Start();

                var semaphoreSlimThread = new Thread(SemaphoreSlimThread);
                semaphoreSlimThread.Start();

                var rwLockThread = new Thread(ReaderWriterLockThread);
                rwLockThread.Start();

                var rwLockSlimThread = new Thread(ReaderWriterLockSlimThread);
                rwLockSlimThread.Start();

                // wait for all threads to finish
                owningThread.Join();
                autoResetEventThread.Join();
                manualResetEventThread.Join();
                mutexThread.Join();
                semaphoreThread.Join();
                semaphoreSlimThread.Join();
                rwLockThread.Join();
                rwLockSlimThread.Join();

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
            semaphoreSlim.Wait();
            rwlock.AcquireWriterLock(5000);
            rwlockSlim.EnterWriteLock();

            Thread.Sleep(3000);
            Console.WriteLine();
            Console.WriteLine("    Release resources");

            mutex.ReleaseMutex();
            semaphore.Release(1);
            semaphoreSlim.Release(1);
            rwlock.ReleaseWriterLock();
            rwlockSlim.ExitWriteLock();

            autoResetEvent.Set();
            manualResetEvent.Set();
            manualResetEventSlim.Set();
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ManualResetEventSlimThread()
        {
            try
            {
                Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for ManualResetEventSlim...");
                manualResetEventSlim.Wait();
                manualResetEventSlim.Reset();
                Console.WriteLine("    <-- ManualResetEventSlim");

                throw new Exception("This is a test exception to check the behavior of the profiler");
            }
            catch (Exception)
            {
            }
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SemaphoreSlimThread()
        {
            try
            {
                Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for SemaphoreSlim");
                semaphoreSlim.Wait();
                semaphoreSlim.Release(1);
                Console.WriteLine("    <-- SemaphoreSlim");

                throw new Exception("This is a test exception to check the behavior of the profiler");
            }
            catch (Exception)
            {
            }
        }

        private static void ReaderWriterLockThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for ReaderWriteLock");
            rwlock.AcquireReaderLock(5000);
            rwlock.ReleaseReaderLock();
            Console.WriteLine("    <-- ReaderWriterLock");
        }

        private static void ReaderWriterLockSlimThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for ReaderWriteLockSlim");
            rwlockSlim.EnterReadLock();
            rwlockSlim.ExitReadLock();
            Console.WriteLine("    <-- ReaderWriterLockSlim");
        }


        private static void ParseCommandLine(string[] args, out int iterations)
        {
            iterations = 1;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if ("--iterations".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var number))
                    {
                        if (number <= 0)
                        {
                            throw new ArgumentOutOfRangeException($"Invalid iterations count '{number}': must be > 0");
                        }

                        iterations = number;
                    }
                }
            }
        }

        private static int GetCurrentThreadId()
        {
            return Thread.CurrentThread.ManagedThreadId;
        }
    }
}
