using System;
using System.Net;
using System.Threading;

namespace Samples.RuntimeMetrics
{
    internal static class Program
    {
        private static void Main()
        {
            // Force the tracer to be loaded
            _ = WebRequest.CreateHttp("http://localhost/");

            new Thread(ThrowExceptions) { IsBackground = true }.Start();

            Thread.Sleep(20000);
        }

        private static void ThrowExceptions()
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

                Thread.Sleep(500);
            }
        }
    }
}
