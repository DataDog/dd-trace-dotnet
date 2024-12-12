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
            sw.Start();

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

        static void OwningThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] Start to hold resources");
            Console.WriteLine("___________________________________________");
            mutex.WaitOne();
            semaphore.WaitOne();

            Thread.Sleep(6000);
            Console.WriteLine();
            Console.WriteLine("    Release resources");

            mutex.ReleaseMutex();
            semaphore.Release(1);
            autoResetEvent.Set();
            manualResetEvent.Set();
        }

        static void AutoResetEventThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for AutoResetEvent...");
            autoResetEvent.WaitOne();
            Console.WriteLine("    <-- AutoResetEvent");
        }

        static void ManualResetEventThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for ManualResetEvent...");
            manualResetEvent.WaitOne();
            Console.WriteLine("    <-- ManualResetEvent");
        }

        static void MutexThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for Mutex...");
            mutex.WaitOne();
            Console.WriteLine("    <-- Mutex");
        }

        static void SemaphoreThread()
        {
            Console.WriteLine($"    [{GetCurrentThreadId(),8}] waiting for Semaphore");
            semaphore.WaitOne();
            Console.WriteLine("    <-- Semaphore");
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }

}
