using Datadog.DynamicDiagnosticSourceBindings;
using System;

namespace DemoNetCore31
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DemoNetCore31");

            // ActivityStub activity = ActivityStub.StartNewActivity("a");

            //var a = new System.Diagnostics.Activity("a2");
            //Console.WriteLine($"a.IdFormat: {a.IdFormat}");

            Console.WriteLine("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
