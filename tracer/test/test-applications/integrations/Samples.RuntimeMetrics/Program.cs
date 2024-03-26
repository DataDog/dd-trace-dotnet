using System;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace Samples.RuntimeMetrics
{
    internal static class Program
    {
        private static readonly object SyncRoot = new object();

        private static void Main()
        {
            Console.WriteLine($"Started - PID {Process.GetCurrentProcess().Id}");

            // Force the tracer to be loaded
            _ = WebRequest.CreateHttp("http://localhost/");

            Monitor.Enter(SyncRoot);

            new Thread(GenerateMemoryPressure) { IsBackground = true }.Start();
            new Thread(GenerateEvents) { IsBackground = true }.Start();

            /*
             * We need runtime metrics to be refreshed at least twice to have the contention metrics
             * (because it needs a previous value to compute the variation).
             * Because of the asynchronous initialization of the performance counters, we sometimes
             * miss the first refresh. 
             * 
             * We wait 30 seconds here to ensure that we'll have at least two refreshes.
             */

            Thread.Sleep(30000);

            Console.WriteLine("Exiting");
        }

        private static void GenerateEvents()
        {
            while (true)
            {
                try
                {
                    throw new InvalidOperationException("This is expected");
                }
                catch
                {
                }

                // Sleep for 500ms while creating contention
                Monitor.TryEnter(SyncRoot, 500);
            }
        }

        private static void GenerateMemoryPressure()
        {
            while (true)
            {
                // Do some big allocating etc to ensure committed memory increases
                // over time
                var bigBuffer = new byte[100_000_000];
                new Random().NextBytes(bigBuffer);

                Thread.Sleep(5_000);
            }
        }
    }
}
