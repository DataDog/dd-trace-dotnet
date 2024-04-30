// <copyright file="ExecutorExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// System.Int32 Microsoft.VisualStudio.TestPlatform.CommandLine.Executor::Execute(System.String[]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["vstest.console", "vstest.console.arm64"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.CommandLine.Executor",
    MethodName = "Execute",
    ReturnTypeName = ClrNames.Int32,
    ParameterTypeNames = ["System.String[]"],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExecutorExecuteIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref string[]? args)
    {
        if (!DotnetCommon.DotnetTestIntegrationEnabled)
        {
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, DotnetCommon.CreateSession());
    }

    internal static CallTargetReturn<int> OnMethodEnd<TTarget>(int returnValue, Exception? exception, in CallTargetState state)
    {
        DotnetCommon.FinalizeSession(state.State as TestSession, returnValue, exception);
        return new CallTargetReturn<int>(returnValue);
    }
}
