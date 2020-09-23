using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Benchmarks.Trace.microbenchmarks;
using Datadog.Trace.BenchmarkDotNet;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static void Main(string[] args)
        {
#if DEBUG
            Console.WriteLine(new DbCommandBenchmark().ExecuteNonQuery_Old());
            Console.WriteLine(new DbCommandBenchmark().ExecuteNonQuery_New());
#endif

#if !NETFRAMEWORK && DEBUG
            // new SpanBenchmark().StartFinishSpanWithTag();
            // new HttpClientBenchmark().SendAsync_New().GetAwaiter().GetResult();
#endif
            var config = DefaultConfig.Instance.AddExporter(DatadogExporter.Default);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
