using System;

using OpenTelemetry.DynamicActivityBinding;

namespace DemoNetFx45
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DemoNetFx45");

            //var a = new System.Diagnostics.Activity("a");

            ActivityStub activity = ActivityStub.StartNewActivity("a");

            Console.WriteLine("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
