using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var config = DefaultConfig.Instance.AddExporter(DatadogExporter.Default);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
