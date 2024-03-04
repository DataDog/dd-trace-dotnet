// <copyright file="TestExtensionsSetBenchmarkMetadataIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci;

/// <summary>
/// Datadog.Trace.Ci.TestExtensions::SetBenchmarkMetadata() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Ci.TestExtensions",
    MethodName = "SetBenchmarkMetadata",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Datadog.Trace.Ci.ITest", "Datadog.Trace.Ci.BenchmarkHostInfo&", "Datadog.Trace.Ci.BenchmarkJobInfo&"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestExtensionsSetBenchmarkMetadataIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTest, THostInfo, TJobInfo>(TTest test, in THostInfo hostInfo, in TJobInfo jobInfo)
        where THostInfo : IBenchmarkHostInfo
        where TJobInfo : IBenchmarkJobInfo
    {
        // Test is an ITest, so it could be something arbitrary - if so, we just ignore it.
        // This is not ideal, but these methods can be directly duck typed using the same "shape" as Test,
        // so it's the lesser of two evils.

        if (test is IDuckType { Instance: Test automaticTest })
        {
            var host = new BenchmarkHostInfo
            {
                ProcessorName = hostInfo.ProcessorName,
                ProcessorCount = hostInfo.ProcessorCount,
                PhysicalCoreCount = hostInfo.PhysicalCoreCount,
                LogicalCoreCount = hostInfo.LogicalCoreCount,
                ProcessorMaxFrequencyHertz = hostInfo.ProcessorMaxFrequencyHertz,
                OsVersion = hostInfo.OsVersion,
                RuntimeVersion = hostInfo.RuntimeVersion,
                ChronometerFrequencyHertz = hostInfo.ChronometerFrequencyHertz,
                ChronometerResolution = hostInfo.ChronometerResolution,
            };

            var job = new BenchmarkJobInfo()
            {
                Description = jobInfo.Description,
                Platform = jobInfo.Platform,
                RuntimeName = jobInfo.RuntimeName,
                RuntimeMoniker = jobInfo.RuntimeMoniker,
            };

            automaticTest.SetBenchmarkMetadata(in host, in job);
        }

        return CallTargetState.GetDefault();
    }
}
