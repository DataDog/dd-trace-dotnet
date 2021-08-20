using System;
using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.Slimple.NetCore31
{
    public class Program
    {
        // This demo shows one of several possible ways for dealing with dynamic invocation exceptions.
        // The corresponding Net Fx demo shows how to use the APIs directly.
        // Other demos show other approaches for dealing with these exceptions.

        public static void Main(string[] _)
        {
            ConsoleWrite.Line(typeof(Program).FullName);

            //UseDiagnosticSource.Run();
            UseDiagnosticSourceStub.Run();

            ConsoleWrite.Line("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
