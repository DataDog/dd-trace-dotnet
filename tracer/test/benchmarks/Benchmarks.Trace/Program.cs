using System;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using Benchmarks.Trace.Jetbrains;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine($"Execution context: ");
            Console.WriteLine("CurrentCulture is {0}.", CultureInfo.CurrentCulture.Name);

            var config = DefaultConfig.Instance;

            const string jetBrainsDotTrace = "-jetbrains:dottrace";
            const string jetBrainsDotTraceTimeline = "-jetbrains:dottrace:timeline";
            const string jetBrainsDotMemory = "-jetbrains:dotmemory";

            if (args?.Any(a => a == jetBrainsDotTrace) == true)
            {
                Console.WriteLine("Setting Jetbrains trace collection... (could take time downloading collector binaries)");
                args = args.Where(a => a != jetBrainsDotTrace).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.Trace);
            }
            else if (args?.Any(a => a == jetBrainsDotTraceTimeline) == true)
            {
                Console.WriteLine("Setting Jetbrains timeline trace collection... (could take time downloading collector binaries)");
                args = args.Where(a => a != jetBrainsDotTraceTimeline).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.TimelineTrace);
            }
            else if (args?.Any(a => a == jetBrainsDotMemory) == true)
            {
                Console.WriteLine("Setting Jetbrains memory collection... (could take time downloading collector binaries)");
                args = args.Where(a => a != jetBrainsDotMemory).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.Memory);
            }
            
            config = config.WithDatadog()
                           .AddExporter(JsonExporter.FullCompressed);
            
            var agentName = Environment.GetEnvironmentVariable("AGENT_NAME");
            if (Enum.TryParse(agentName, out AgentFilterAttribute.Agent benchmarkAgent))
            {
                var attributeName = $"{benchmarkAgent}Attribute";
                Console.WriteLine($"Found agent name {agentName}; executing only benchmarks decorated with '{attributeName}");
                config = config.AddFilter(new AttributesFilter(new[] { attributeName }));
            }
            else
            {
                Console.WriteLine($"Unknown agent name {agentName}; executing all benchmarks");
            }

            Console.WriteLine("Running tests...");
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
