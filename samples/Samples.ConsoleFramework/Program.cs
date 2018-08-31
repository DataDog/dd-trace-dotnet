using System;

namespace Samples.ConsoleFramework
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new Program().Run();
        }

        private void Run()
        {
            Console.WriteLine(new ExampleLibrary.Class1().Add(1, 2));
            Console.ReadLine();
        }
    }
}
