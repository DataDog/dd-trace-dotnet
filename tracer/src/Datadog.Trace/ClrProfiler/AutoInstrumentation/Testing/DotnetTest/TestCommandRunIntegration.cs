// <copyright file="TestCommandRunIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// System.Int32 Microsoft.DotNet.Tools.Test.TestCommand::Run(System.CommandLine.ParseResult or string[]) and
/// Microsoft.DotNet.Cli.Commands.Test.TestCommand::Run(System.CommandLine.ParseResult) calltarget instrumentation
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
[InstrumentMethod(
    AssemblyName = "dotnet",
    TypeName = "Microsoft.DotNet.Cli.Commands.Test.TestCommand",
    MethodName = "Run",
    ReturnTypeName = ClrNames.Int32,
    ParameterTypeNames = ["System.CommandLine.ParseResult"],
    MinimumVersion = "10.0.0",
    MaximumVersion = "10.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TestCommandRunIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TParseResultOrStringArray>(ref TParseResultOrStringArray? parseResult)
    {
        if (!DotnetCommon.DotnetTestIntegrationEnabled)
        {
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(null, DotnetCommon.CreateRunState(DotnetTestCommandKind.DotnetTestCommand));
    }

    internal static CallTargetReturn<int> OnMethodEnd<TTarget>(int returnValue, Exception? exception, in CallTargetState state)
    {
        DotnetCommon.FinalizeRunState(state.State as DotnetTestRunState, returnValue, exception);
        return new CallTargetReturn<int>(returnValue);
    }
}
