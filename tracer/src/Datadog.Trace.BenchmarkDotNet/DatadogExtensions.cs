// <copyright file="DatadogExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Datadog extensions
/// </summary>
public static class DatadogExtensions
{
    /// <summary>
    /// Configure the Datadog Exporter, Diagnoser and Column hiding rule
    /// </summary>
    /// <param name="config">Configuration instance</param>
    /// <returns>Same configuration instance</returns>
    public static IConfig WithDatadog(this IConfig config)
        => config.WithDatadog(enableProfiler: null);

    /// <summary>
    /// Configure the Datadog Exporter, Diagnoser and Column hiding rule
    /// </summary>
    /// <param name="config">Configuration instance</param>
    /// <param name="enableProfiler">True to enable Datadog's Profiler; a null value will parse DD_PROFILING_ENABLED environment variable</param>
    /// <returns>Same configuration instance</returns>
    internal static IConfig WithDatadog(this IConfig config, bool? enableProfiler)
    {
        var cfg = config.AddLogger(DatadogSessionLogger.Default);

        enableProfiler ??= (EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.Profiler.ProfilingEnabled) ?? string.Empty).ToBoolean() ?? false;
        switch (enableProfiler)
        {
            case true:
                cfg = cfg.WithOption(ConfigOptions.KeepBenchmarkFiles, true);
                break;
            case false:
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Profiler.ProfilingEnabled, null);
                break;
        }

        return cfg.AddDiagnoser(new DatadogDiagnoser(enableProfiler.Value));
    }

    /// <summary>
    /// Adds a filter that skips benchmark classes whose body is a stub on the
    /// job's runtime, as marked by <see cref="MinRuntimeAttribute"/>
    /// </summary>
    /// <param name="config">Configuration instance</param>
    /// <returns>Same configuration instance</returns>
    public static IConfig AddMinRuntimeFilter(this IConfig config)
        => config.AddFilter(new SimpleFilter(HasRealBody));

    /// <summary>
    /// Skips a benchmark class when <see cref="MinRuntimeAttribute"/> marks
    /// the job's runtime as older than its real body. Classes without the
    /// attribute always run.
    /// </summary>
    private static bool HasRealBody(BenchmarkCase benchmarkCase)
    {
        var minimum = benchmarkCase.Descriptor.Type.GetCustomAttribute<MinRuntimeAttribute>(inherit: false);

        if (minimum is null)
        {
            return true;
        }

        // BenchmarkDotNet 0.15.8 keeps RuntimeInformation.GetCurrentRuntime()
        // internal; EnvironmentResolver is the public path to the same value
        // for jobs that do not pin a runtime.
        var runtime = benchmarkCase.Job.Environment.Runtime
            ?? benchmarkCase.Job.Environment.ResolveValue(EnvironmentMode.RuntimeCharacteristic, EnvironmentResolver.Instance);

        var moniker = runtime.RuntimeMoniker;

        // NotRecognized means BenchmarkDotNet predates the job's TFM (e.g.
        // .NET 11 on 0.15.8) and sorts below Net461, so the ordinal check
        // would skip real benchmarks; run them instead. The enum is ordered
        // for version comparison by BenchmarkDotNet itself.
        if (moniker == RuntimeMoniker.NotRecognized)
        {
            return true;
        }

        return moniker >= minimum.Minimum;
    }
}
