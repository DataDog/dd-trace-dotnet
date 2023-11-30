using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Console_
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // Just to make extra sure that the tracer is loaded, if properly configured
            _ = WebRequest.CreateHttp("http://localhost");
            await Task.Yield();

            Console.WriteLine($"Waiting - PID: {Process.GetCurrentProcess().Id} - Profiler attached: {SampleHelpers.IsProfilerAttached()}");

            if (args.Length > 0)
            {
                Console.WriteLine($"Args: {string.Join(" ", args)}");

                if (string.Equals(args[0], "traces", StringComparison.OrdinalIgnoreCase))
                {
                    var count = int.Parse(args[1]);

                    Console.WriteLine($"Sending {count} spans");

                    for (int i = 0; i < count; i++)
                    {
                        SampleHelpers.CreateScope("test").Dispose();
                    }

                    await SampleHelpers.ForceTracerFlushAsync();
                    return;
                }

                if (string.Equals(args[0], "echo", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Ready");
                    Console.WriteLine(Console.ReadLine());
                }

                if (string.Equals(args[0], "wait", StringComparison.OrdinalIgnoreCase))
                {
                    Thread.Sleep(Timeout.Infinite);
                }
            }
        }
    }
}
