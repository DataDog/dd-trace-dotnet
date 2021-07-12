using System;
using System.Net;
using System.Threading;

namespace Samples.RuntimeMetrics
{
    internal static class Program
    {
        private static readonly object SyncRoot = new object();

        private static void Main()
        {
            // Force the tracer to be loaded
            _ = WebRequest.CreateHttp("http://localhost/");

            Monitor.Enter(SyncRoot);

            new Thread(GenerateEvents) { IsBackground = true }.Start();

            Thread.Sleep(30000);
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
    }
}
