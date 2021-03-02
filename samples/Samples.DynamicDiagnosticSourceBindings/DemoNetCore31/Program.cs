using System;

namespace DemoNetCore31
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DemoNetCore31");

            //UseDiagnosticSource.Run();
            UseDiagnosticSourceStub.Run();

            Console.WriteLine("Done. Press enter.");
            Console.ReadLine();
        }
    }
}
