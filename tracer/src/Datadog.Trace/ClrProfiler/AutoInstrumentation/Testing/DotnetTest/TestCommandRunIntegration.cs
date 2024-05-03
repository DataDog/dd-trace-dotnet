// <copyright file="TestCommandRunIntegration.cs" company="Datadog">
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
/// System.Int32 Microsoft.DotNet.Tools.Test.TestCommand::Run(System.CommandLine.ParseResult or string[]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "dotnet",
    TypeName = "Microsoft.DotNet.Tools.Test.TestCommand",
    MethodName = "Run",
    ReturnTypeName = ClrNames.Int32,
    ParameterTypeNames = ["_"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "9.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestCommandRunIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TParseResultOrStringArray>(ref TParseResultOrStringArray? parseResult)
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
