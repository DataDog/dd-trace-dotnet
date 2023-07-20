// <copyright file="XUnitTestRunnerRunAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Sdk.TestRunner`1.RunAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
    TypeName = "Xunit.Sdk.TestRunner`1",
    MethodName = "RunAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Xunit.Sdk.RunSummary]",
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestRunnerRunAsyncIntegration
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        if (XUnitIntegration.IsEnabled && instance is not null)
        {
            var runnerInstance = instance.DuckCast<TestRunnerStruct>();

            // Check if the test should be skipped by the ITR
            if (XUnitIntegration.ShouldSkip(ref runnerInstance) && instance.TryDuckCast<ITestRunnerSkippable>(out var skippableRunnerInstance))
            {
                Common.Log.Debug("ITR: Test skipped: {Class}.{Name}", runnerInstance.TestClass?.FullName ?? string.Empty, runnerInstance.TestMethod?.Name ?? string.Empty);
                // Refresh values after skip reason change, and create Skip by ITR span.
                runnerInstance.SkipReason = "Skipped by the Intelligent Test Runner";
                skippableRunnerInstance.SkipReason = runnerInstance.SkipReason;
                XUnitIntegration.CreateTest(ref runnerInstance, instance.GetType());
            }
            else if (runnerInstance.SkipReason is not null)
            {
                // Skip test support
                XUnitIntegration.CreateTest(ref runnerInstance, instance.GetType());
            }
        }

        return CallTargetState.GetDefault();
    }
}
