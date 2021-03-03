using System;
using DynamicDiagnosticSourceBindings.Demo;

namespace Demo.Slimple.NetFx45
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleWrite.Line(typeof(Program).FullName);

            UseDiagnosticSourceStub.Run();

            ConsoleWrite.Line("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
