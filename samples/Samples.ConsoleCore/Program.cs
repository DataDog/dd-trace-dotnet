using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Samples.ConsoleCore
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new Program().Run();
        }

        private void Run()
        {
            var prefixes = new[] { "COR_", "CORECLR_", "DATADOG_" };

            var environmentVariables = from entry in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                                       from prefix in prefixes
                                       let pair = new KeyValuePair<string, string>(
                                           ((string)entry.Key).ToUpperInvariant(),
                                           (string)entry.Value)
                                       where pair.Key.StartsWith(prefix)
                                       orderby pair.Key
                                       select pair;

            foreach (var envVar in environmentVariables)
            {
                Console.WriteLine($"{envVar.Key}={envVar.Value}");
            }

            Console.WriteLine($"ProfilerAttached={Datadog.Trace.ClrProfiler.Instrumentation.ProfilerAttached}");
            Console.WriteLine($"Add(1,2)={new ExampleLibrary.Class1().Add(1, 2)}");
        }
    }
}
