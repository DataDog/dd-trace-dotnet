// <copyright file="DatadogExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using BenchmarkDotNet.Configs;

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
    {
        return config.AddDiagnoser(DatadogDiagnoser.Default)
                     .AddLogger(DatadogSessionLogger.Default);
    }
}
