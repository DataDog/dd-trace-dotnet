using System;
using System.IO;
using System.Net;
using Mono.Cecil;

namespace Datadog.Trace.ClrProfiler.Generator
{
    /// <summary>
    /// Entrypoint for the generator
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main method
        /// </summary>
        /// <param name="args">args sent to the program</param>
        public static void Main(string[] args)
        {
            if (!Path.Exists("packages/StackExchange.Redis-2.0.513.nupkg"))
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(
                        "https://www.nuget.org/api/v2/package/StackExchange.Redis/2.0.513",
                        "packages/StackExchange.Redis-2.0.513.nupkg");
                }
            }

            var module = ModuleDefinition.ReadModule("StackExchange.Redis.dll");
            Console.WriteLine(module);
        }
    }
}
