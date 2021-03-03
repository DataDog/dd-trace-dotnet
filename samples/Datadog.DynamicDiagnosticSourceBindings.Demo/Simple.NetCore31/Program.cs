using System;
using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.Slimple.NetCore31
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleWrite.Line(typeof(Program).FullName);

            //UseDiagnosticSource.Run();
            UseDiagnosticSourceStub.Run();

            ConsoleWrite.Line("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
