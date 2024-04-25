// <copyright file="SimpleWorkItemctor2Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// System.Void NUnit.Framework.Internal.Execution.SimpleWorkItem::.ctor(NUnit.Framework.Internal.TestMethod,NUnit.Framework.Interfaces.ITestFilter,NUnit.Framework.Internal.Abstractions.IDebugger) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Execution.SimpleWorkItem",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["NUnit.Framework.Internal.TestMethod", "NUnit.Framework.Interfaces.ITestFilter", "NUnit.Framework.Internal.Abstractions.IDebugger"],
    MinimumVersion = "3.13.0",
    MaximumVersion = "4.*.*",
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SimpleWorkItemctor2Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTest, TFilter, TDebugger>(TTarget instance, ref TTest? test, ref TFilter? filter, ref TDebugger? debugger)
    {
        NUnitIntegration.IncrementTotalTestCases();
        return CallTargetState.GetDefault();
    }
}
