// <copyright file="TestRunRequestExecuteAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// Captures the session before VSTest dispatches its asynchronous completion callback.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.Client",
    TypeName = "Microsoft.VisualStudio.TestPlatform.Client.Execution.TestRunRequest",
    MethodName = "ExecuteAsync",
    ReturnTypeName = ClrNames.Int32,
    ParameterTypeNames = [],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TestRunRequestExecuteAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        if (DotnetCommon.DotnetTestIntegrationEnabled && instance is not null)
        {
            VSTestRunTracker.Start(instance, TestSession.Current);
        }

        return CallTargetState.GetDefault();
    }
}
