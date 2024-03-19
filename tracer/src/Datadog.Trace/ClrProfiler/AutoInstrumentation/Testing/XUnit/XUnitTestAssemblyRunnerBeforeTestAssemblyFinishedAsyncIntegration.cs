// <copyright file="XUnitTestAssemblyRunnerBeforeTestAssemblyFinishedAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Sdk.TestAssemblyRunner`1.BeforeTestAssemblyFinishedAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["xunit.execution.dotnet", "xunit.execution.desktop"],
    TypeName = "Xunit.Sdk.TestAssemblyRunner`1",
    MethodName = "BeforeTestAssemblyFinishedAsync",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[InstrumentMethod(
    AssemblyNames = ["xunit.execution.dotnet", "xunit.execution.desktop"],
    TypeName = "Xunit.Sdk.TestAssemblyRunner`1",
    MethodName = "BeforeTestAssemblyFinishedAsync",
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName,
    CallTargetIntegrationKind = CallTargetKind.Derived)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestAssemblyRunnerBeforeTestAssemblyFinishedAsyncIntegration
{
    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return type</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (TestModule.Current is { } testModule)
        {
            await testModule.CloseAsync().ConfigureAwait(false);

            // Because we are auto-instrumenting a VSTest testhost process we need to manually call the shutdown process
            CIVisibility.Close();
        }

        return returnValue;
    }
}
