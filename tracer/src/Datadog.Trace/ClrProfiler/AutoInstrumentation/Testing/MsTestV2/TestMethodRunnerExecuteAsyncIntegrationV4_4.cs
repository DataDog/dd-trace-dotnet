// <copyright file="TestMethodRunnerExecuteAsyncIntegrationV4_4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Marks an attempt around all its data rows, rather than counting each row as a retry.
/// </summary>
[InstrumentMethod(
    AssemblyName = "MSTestAdapter.PlatformServices",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.VisualStudio.TestTools.UnitTesting.TestResult[]]",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.String],
    MinimumVersion = "4.4.0",
    MaximumVersion = "4.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestMethodRunnerExecuteAsyncIntegrationV4_4
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string? logs, string? errors, string? trace, string? messages)
    {
        MsTestExecution.Current?.StartNativeAttempt();
        return CallTargetState.GetDefault();
    }
}
