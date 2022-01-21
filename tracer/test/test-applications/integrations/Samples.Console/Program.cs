using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Console_
{
    internal static class Program
    {
        private static async Task Main()
        {
            // Just to make extra sure that the tracer is loaded, if properly configured
            _ = WebRequest.CreateHttp("http://localhost");
            await Task.Yield();
            
            Console.WriteLine($"Waiting - PID: {Process.GetCurrentProcess().Id}");
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
