using System;

namespace Samples.ConsoleDeadLock
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            // If we reach here, the tracer initialized without deadlocking.
            // The config builder in App.config fires during tracer initialization
            // (CLR processes app.config when loading Datadog.Trace.dll).
            // If the deadlock occurs, the process hangs and never prints this line.
            Console.WriteLine("Main() reached - no deadlock!");
            Console.WriteLine("Program completed successfully.");
        }
    }
}
