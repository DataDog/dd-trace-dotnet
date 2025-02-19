// <copyright file="TestAssemblyInfoGetAssemblyCleanupMethodIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo.get_AssemblyCleanupMethod() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
    TypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo",
    MethodName = "get_AssemblyCleanupMethod",
    ReturnTypeName = "_",
    MinimumVersion = "14.0.0",
    MaximumVersion = "14.*.*",
    IntegrationName = MsTestIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TestAssemblyInfoGetAssemblyCleanupMethodIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        where TTarget : ITestAssemblyInfo
    {
        if (MsTestIntegration.IsEnabled && MsTestIntegration.GetOrCreateTestModuleFromTestAssemblyInfo(instance, null) is { } module)
        {
            module.Close();

            // Because we are auto-instrumenting a VSTest testhost process we need to manually call the shutdown process
            CIVisibility.Close();
        }

        return CallTargetState.GetDefault();
    }
}
