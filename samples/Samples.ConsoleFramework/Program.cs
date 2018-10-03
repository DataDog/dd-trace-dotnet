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
            Console.WriteLine($"1 + 2 = {new ExampleLibrary.Class1().Add(1, 2)}");
            Console.WriteLine($"1 * 2 = {new ExampleLibrary.Class1().Multiply(1, 2)}");
            Console.WriteLine($"1 / 2 = {new ExampleLibrary.Class1().Divide(1, 2)}");
            Console.WriteLine($"{new ExampleLibrary.Class1().Example("x", "y")}");
            Console.ReadLine();
        }
    }
}
