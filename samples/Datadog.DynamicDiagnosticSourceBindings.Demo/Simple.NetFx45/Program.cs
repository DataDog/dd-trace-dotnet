using System;

namespace Demo.Slimple.NetFx45
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DemoNetFx45");

            UseDiagnosticSourceStub.Run();

            Console.WriteLine("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
