// <copyright file="TestCommandctorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// System.Void Microsoft.DotNet.Tools.Test.TestCommand::.ctor(System.Collections.Generic.IEnumerable`1[System.String],System.Boolean,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "dotnet",
    TypeName = "Microsoft.DotNet.Tools.Test.TestCommand",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[System.String]", ClrNames.Bool, ClrNames.String],
    MinimumVersion = "6.0.0",
    MaximumVersion = "9.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestCommandctorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable<string>? msbuildArgs, ref bool noRestore, ref string? msbuildPath)
    {
        if (!DotnetCommon.DotnetTestIntegrationEnabled || CIVisibility.Settings.CodeCoverageEnabled != true || msbuildArgs is null)
        {
            return CallTargetState.GetDefault();
        }

        DotnetCommon.InjectCodeCoverageCollectorToDotnetTest(ref msbuildArgs);
        DotnetCommon.WriteDebugInfoForDotnetTest(msbuildArgs, null, null, noRestore, msbuildPath);
        return CallTargetState.GetDefault();
    }
}
