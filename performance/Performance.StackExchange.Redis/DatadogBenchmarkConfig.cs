using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace Performance.StackExchange.Redis
{
    public class DatadogBenchmarkConfig : ManualConfig
    {
        public DatadogBenchmarkConfig()
        {
            Add(MemoryDiagnoser.Default);
#if NETCOREAPP3_0
            Add(ThreadingDiagnoser.Default);
#endif
            Add(new Job
            {
                Run = { LaunchCount = 1, WarmupCount = 0, IterationCount = 100 }
            });
        }
    }
}
