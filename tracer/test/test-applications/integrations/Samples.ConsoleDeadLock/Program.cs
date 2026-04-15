using System;

namespace Samples.ConsoleDeadLock
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            // If we reach here, the tracer initialized without deadlocking.
            // The config builder in App.config fires during ConfigurationManager.AppSettings
            // access, which happens during tracer initialization (before Main runs).
            // If the tracer's HTTP instrumentation causes re-entrant config access,
            // the process hangs and never prints this line.
            Console.WriteLine("Main() reached - no deadlock!");

            // Access AppSettings explicitly to confirm config builder ran
            var value = System.Configuration.ConfigurationManager.AppSettings["TestKey"];
            Console.WriteLine($"AppSettings[\"TestKey\"] = {value ?? "(null)"}");

            Console.WriteLine("Program completed successfully.");
        }
    }
}
