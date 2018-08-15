using System;
using Newtonsoft.Json;

namespace Samples.ConsoleFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            var str = JsonConvert.SerializeObject(args);
            Console.WriteLine($"ARGS: {str}");
            Console.ReadLine();
        }
    }
}
