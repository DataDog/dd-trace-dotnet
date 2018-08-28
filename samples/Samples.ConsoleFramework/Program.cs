using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Samples.ConsoleFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().Run();
        }

        void Run()
        {
            Console.WriteLine(new ExampleLibrary.Class1().Add(1, 2));
            Console.ReadLine();
        }
    }
}
