// <copyright file="TestExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci;

/// <summary>
/// Extension methods for adding data to <see cref="ITest"/> instances
/// </summary>
public static class TestExtensions
{
    /// <summary>
    /// Set Test parameters
    /// </summary>
    /// <param name="test">The <see cref="ITest"/> instance to augment</param>
    /// <param name="parameters">TestParameters instance</param>
    [Instrumented]
    public static void SetParameters(this ITest test, TestParameters parameters)
    {
        if (Instrumentation.SafeIsManualInstrumentationOnly())
        {
            TestExtensionsSetParametersIntegration.OnMethodBegin<object, ITest, ITestParameters>(test, parameters.DuckCast<ITestParameters>());
        }
    }

    /// <summary>
    /// Set benchmark metadata
    /// </summary>
    /// <param name="test">The <see cref="ITest"/> instance to augment</param>
    /// <param name="hostInfo">Host info</param>
    /// <param name="jobInfo">Job info</param>
    [Instrumented]
    public static void SetBenchmarkMetadata(this ITest test, in BenchmarkHostInfo hostInfo, in BenchmarkJobInfo jobInfo)
    {
        if (Instrumentation.SafeIsManualInstrumentationOnly())
        {
            TestExtensionsSetBenchmarkMetadataIntegration.OnMethodBegin<object, ITest, IBenchmarkHostInfo, IBenchmarkJobInfo>(
                test,
                hostInfo.DuckCast<IBenchmarkHostInfo>(),
                jobInfo.DuckCast<IBenchmarkJobInfo>());
        }
    }

    /// <summary>
    /// Add benchmark data
    /// </summary>
    /// <param name="test">The <see cref="ITest"/> instance to augment</param>
    /// <param name="measureType">Measure type</param>
    /// <param name="info">Measure info</param>
    /// <param name="statistics">Statistics values</param>
    [Instrumented]
    public static void AddBenchmarkData(this ITest test, BenchmarkMeasureType measureType, string info, in BenchmarkDiscreteStats statistics)
    {
        if (Instrumentation.SafeIsManualInstrumentationOnly())
        {
            TestExtensionsAddBenchmarkDataIntegration.OnMethodBegin<object, ITest, IBenchmarkDiscreteStats>(
                test,
                measureType,
                info,
                statistics.DuckCast<IBenchmarkDiscreteStats>());
        }
    }
}
