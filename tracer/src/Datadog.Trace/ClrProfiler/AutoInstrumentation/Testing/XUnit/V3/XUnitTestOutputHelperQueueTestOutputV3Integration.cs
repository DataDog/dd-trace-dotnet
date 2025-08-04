// <copyright file="XUnitTestOutputHelperQueueTestOutputV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestOutputHelper.QueueTestOutput calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.TestOutputHelper",
    MethodName = "QueueTestOutput",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String],
    MinimumVersion = "1.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestOutputHelperQueueTestOutputV3Integration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string output)
    {
        return XUnitTestOutputHelperQueueTestOutputIntegration.OnMethodBegin(instance, output);
    }
}
