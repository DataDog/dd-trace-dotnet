using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Console_
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0].StartsWith("crash"))
            {
                Ready();

                var exception = args[0] == "crash-datadog" ? (Exception)new BadImageFormatException("Expected") : new InvalidOperationException("Expected");
                DumpCallstackAndThrow(exception);
            }
            else
            {
                AsyncMain(args).GetAwaiter().GetResult();
            }
        }

        // Can't use a "real" async Main because it messes up the callstack for the crash-report tests
        private static async Task AsyncMain(string[] args)
        {
            // Just to make extra sure that the tracer is loaded, if properly configured
            _ = WebRequest.CreateHttp("http://localhost");
            await Task.Yield();

            Ready();

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
                    Console.WriteLine($"Echo: {Console.ReadLine()}");
                    return;
                }

                if (string.Equals(args[0], "wait", StringComparison.OrdinalIgnoreCase))
                {
                    Thread.Sleep(Timeout.Infinite);
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DumpCallstackAndThrow(Exception exception)
        {
            var stackTrace = new StackTrace();

            foreach (var frame in stackTrace.GetFrames())
            {
                var method = frame.GetMethod();

                var methodName = method.Module.Assembly == typeof(Program).Assembly ? "REDACTED" : $"{method.DeclaringType.FullName}.{method.Name}"; 
                Console.WriteLine($"Frame|{Path.GetFileName(method.Module.Assembly.Location)}!{methodName}");
            }

            Console.WriteLine("Crashing...");
            throw exception;
        }

        private static void Ready()
        {
            Console.WriteLine($"Waiting - PID: {Process.GetCurrentProcess().Id} - Profiler attached: {SampleHelpers.IsProfilerAttached()}");
        }
    }
}
