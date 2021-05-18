using System;
using System.Runtime.CompilerServices;

namespace Samples.NoMultiLoader
{
    class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Main(string[] args)
        {
            int count = 2000;
            while (count-- > 0)
            {
                Deps.DatabaseSample.CreateSqlConnection();
            }
            Console.WriteLine("Done.");
        }
    }
}
