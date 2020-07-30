using System;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Summary summary;
            summary = BenchmarkRunner.Run<SpanBenchmark>();
            summary = BenchmarkRunner.Run<TracerBenchmark>();
        }
    }
}
