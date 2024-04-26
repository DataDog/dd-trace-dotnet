// <copyright file="TestCommand5ctorIntegration.cs" company="Datadog">
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
/// System.Void Microsoft.DotNet.Tools.Test.TestCommand::.ctor(System.Collections.Generic.IEnumerable`1[System.String],System.Collections.Generic.IEnumerable`1[System.String],System.Collections.Generic.IEnumerable`1[System.String],System.Boolean,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "dotnet",
    TypeName = "Microsoft.DotNet.Tools.Test.TestCommand",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[System.String]", "System.Collections.Generic.IEnumerable`1[System.String]", "System.Collections.Generic.IEnumerable`1[System.String]", ClrNames.Bool, ClrNames.String],
    MinimumVersion = "2.0.0",
    MaximumVersion = "5.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestCommand5ctorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable<string>? msbuildArgs, ref IEnumerable<string>? userDefinedArguments, ref IEnumerable<string>? trailingArguments, ref bool noRestore, ref string? msbuildPath)
    {
        if (!DotnetCommon.DotnetTestIntegrationEnabled || CIVisibility.Settings.CodeCoverageEnabled != true || msbuildArgs is null)
        {
            return CallTargetState.GetDefault();
        }

        DotnetCommon.InjectCodeCoverageCollectorToDotnetTest(ref msbuildArgs);
        DotnetCommon.WriteDebugInfoForDotnetTest(msbuildArgs, userDefinedArguments, trailingArguments, noRestore, msbuildPath);
        return CallTargetState.GetDefault();
    }
}
