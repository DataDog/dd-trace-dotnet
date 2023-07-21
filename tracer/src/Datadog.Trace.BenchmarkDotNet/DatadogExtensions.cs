// <copyright file="DatadogExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using BenchmarkDotNet.Configs;
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
    /// <param name="enableProfiler">True to enable Datadog's Profiler; a null value will parse DD_PROFILING_ENABLED environment variable</param>
    /// <returns>Same configuration instance</returns>
    public static IConfig WithDatadog(this IConfig config, bool? enableProfiler = null)
    {
        var cfg = config
                     .AddDiagnoser(DatadogDiagnoser.Default);

        enableProfiler ??= EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.ProfilingEnabled, string.Empty).ToBoolean();
        switch (enableProfiler)
        {
            case true:
                cfg = cfg.AddDiagnoser(DatadogProfilerDiagnoser.Default);
                break;
            case false:
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.ProfilingEnabled, null);
                break;
        }

        return cfg;
    }
}
