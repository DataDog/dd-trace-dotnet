using System;
using System.Linq;
using Datadog.Trace;

namespace Samples.FakeAzureAppServices
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            using (var azureTrace = Tracer.Instance.StartActive("fake-azure"))
            {
                Console.WriteLine("Manual custom azure trace started.");

                if (args.Any())
                {
                    for (var i = 0; i < args.Length; i++)
                    {
                        Console.WriteLine($"Setting tag for argument {i}: {args[i]}");
                        azureTrace.Span.SetTag($"arg{i}", args[i]);
                    }
                }

                Console.WriteLine("Manual custom azure trace finished.");
            }
        }
    }
}
