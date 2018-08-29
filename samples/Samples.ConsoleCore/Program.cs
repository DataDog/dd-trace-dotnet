using System;

namespace Samples.ConsoleCore
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new Program().Run();
        }

        private void Run()
        {
            Console.WriteLine($"ProfilerAttached={Datadog.Trace.ClrProfiler.Instrumentation.ProfilerAttached}");
            Console.WriteLine($"Add(1,2)={new ExampleLibrary.Class1().Add(1, 2)}");
        }
    }
}
