using BenchmarkDotNet.Running;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            BenchmarkRunner.Run<SpanBenchmark>();
        }
    }
}
