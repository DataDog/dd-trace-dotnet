// <copyright file="TestExtensionsAddBenchmarkDataIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.StatsdClient.Statistic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci;

/// <summary>
/// Datadog.Trace.Ci.TestExtensions::AddBenchmarkData() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Ci.TestExtensions",
    MethodName = "AddBenchmarkData",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Datadog.Trace.Ci.ITest", "Datadog.Trace.Ci.BenchmarkMeasureType", ClrNames.String, "Datadog.Trace.Ci.BenchmarkDiscreteStats&"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestExtensionsAddBenchmarkDataIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTest, TStats>(TTest test, BenchmarkMeasureType measureType, string info, in TStats statistics)
        where TStats : IBenchmarkDiscreteStats
    {
        // Test is an ITest, so it could be something arbitrary - if so, we just ignore it.
        // This is not ideal, but these methods can be directly duck typed using the same "shape" as Test,
        // so it's the lesser of two evils.

        if (test is IDuckType { Instance: Test automaticTest })
        {
            var stats = new BenchmarkDiscreteStats(
                n: statistics.N,
                max: statistics.Max,
                min: statistics.Min,
                mean: statistics.Mean,
                median: statistics.Median,
                standardDeviation: statistics.StandardDeviation,
                standardError: statistics.StandardError,
                kurtosis: statistics.Kurtosis,
                skewness: statistics.Skewness,
                p99: statistics.P99,
                p95: statistics.P95,
                p90: statistics.P90);

            automaticTest.AddBenchmarkData(measureType, info, in stats);
        }

        return CallTargetState.GetDefault();
    }
}
