// <copyright file="RunTestsWithSourcesSendSessionEndIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution.RunTestsWithSources.SendSessionEnd() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.TestPlatform.CrossPlatEngine",
    TypeName = "Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution.RunTestsWithSources",
    MethodName = "SendSessionEnd",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "14.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class RunTestsWithSourcesSendSessionEndIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        TestOptimization.Instance.Close();
        return CallTargetState.GetDefault();
    }
}
