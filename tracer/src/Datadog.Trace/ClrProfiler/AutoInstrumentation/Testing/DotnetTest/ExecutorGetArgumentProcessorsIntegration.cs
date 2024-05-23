// <copyright file="ExecutorGetArgumentProcessorsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// System.Int32 Microsoft.VisualStudio.TestPlatform.CommandLine.Executor::GetArgumentProcessors(System.String[],System.Collections.Generic.List`1[Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.IArgumentProcessor]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["vstest.console", "vstest.console.arm64"],
    TypeName = "Microsoft.VisualStudio.TestPlatform.CommandLine.Executor",
    MethodName = "GetArgumentProcessors",
    ReturnTypeName = ClrNames.Int32,
    ParameterTypeNames = ["System.String[]", "System.Collections.Generic.List`1[Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.IArgumentProcessor]&"],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExecutorGetArgumentProcessorsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TProcessors>(TTarget instance, ref string[]? args, ref TProcessors? processors)
    {
        if (!DotnetCommon.DotnetTestIntegrationEnabled || CIVisibility.Settings.CodeCoverageEnabled != true || args is null)
        {
            return CallTargetState.GetDefault();
        }

        DotnetCommon.InjectCodeCoverageCollectorToVsConsoleTest(ref args);
        DotnetCommon.WriteDebugInfoForVsConsoleTest(args);
        return CallTargetState.GetDefault();
    }
}
