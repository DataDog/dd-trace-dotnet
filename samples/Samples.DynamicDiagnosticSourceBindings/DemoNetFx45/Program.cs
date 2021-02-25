using System;
using Datadog.DynamicDiagnosticSourceBindings;

namespace DemoNetFx45
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DemoNetFx45");

            UseDiagnosticSource.Run();
            UseDiagnosticSourceStub.Run();

            //var a = new System.Diagnostics.Activity("a");

            //ActivityStub activity = ActivityStub.StartNewActivity("a");

            Console.WriteLine("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
