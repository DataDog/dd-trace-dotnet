using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace Performance.StackExchange.Redis
{
    public class DatadogBenchmarkConfig : ManualConfig
    {
        public DatadogBenchmarkConfig()
        {
            this.With(ConfigOptions.DisableOptimizationsValidator);

            var runtimes = new[] { ClrRuntime.Net48 };

            foreach (var clrRuntime in runtimes)
            {
                Add(new Job(EnvironmentMode.RyuJitX64, RunMode.VeryLong)
                {
                    Environment = { Runtime = clrRuntime },
                    Run = { LaunchCount = 1, WarmupCount = 0, IterationCount = 1000 }
                });
            }
        }
    }
}
