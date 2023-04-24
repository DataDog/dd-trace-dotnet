using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

#nullable enable

namespace Benchmarks.Trace.Jetbrains;

/// <summary>
/// Jetbrains extensions
/// </summary>
public static class JetbrainsExtensions
{
    /// <summary>
    /// Configure the Jetbrains diagnoser
    /// </summary>
    /// <param name="config">Configuration instance</param>
    /// <param name="product">Jetbrains product</param>
    /// <param name="outputFolder">Output folder</param>
    /// <returns>Same configuration instance</returns>
    public static IConfig WithJetbrains(this IConfig config, JetbrainsProduct product, string? outputFolder = null)
    {
        return config
              .AddDiagnoser(new JetbrainsDiagnoser(product, outputFolder))
              .AddJob(Job.InProcess);
    }
}
