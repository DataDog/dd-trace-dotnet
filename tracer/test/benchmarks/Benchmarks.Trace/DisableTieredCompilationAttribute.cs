using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Disables tiered compilation for a benchmark.
    /// Useful for reducing variance in .NET Core 3.1 benchmarks where tiered JIT recompilation
    /// can cause significant run-to-run variance. Not needed for .NET 6+ which has Dynamic PGO.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false)]
    public class DisableTieredCompilationAttribute : Attribute, IConfigSource
    {
        public DisableTieredCompilationAttribute()
        {
            var job = Job.Default
                .WithEnvironmentVariables(
                    new EnvironmentVariable("DOTNET_TieredCompilation", "0"));

            Config = ManualConfig.CreateEmpty().AddJob(job);
        }

        public IConfig Config { get; }
    }
}
