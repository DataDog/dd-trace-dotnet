using System;
using BenchmarkDotNet.Running;

namespace MyBenchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<TracerBenchmarks>();
        }
    }
}