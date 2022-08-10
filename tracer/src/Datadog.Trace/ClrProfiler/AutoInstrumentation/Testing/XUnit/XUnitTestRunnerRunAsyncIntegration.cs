// <copyright file="XUnitTestRunnerRunAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestRunner`1.RunAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        TypeName = "Xunit.Sdk.TestRunner`1",
        MethodName = "RunAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>",
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
            if (XUnitIntegration.IsEnabled)
            {
                TestRunnerStruct runnerInstance = instance.DuckCast<TestRunnerStruct>();

                // Check if the test should be skipped by the ITR
                if (XUnitIntegration.ShouldSkip(ref runnerInstance) && instance.TryDuckCast<ITestRunnerSkippeable>(out var skippeableRunnerInstance))
                {
                    Common.Log.Debug("ITR: Test skipped: {class}.{name}", runnerInstance.TestClass.FullName, runnerInstance.TestMethod.Name);
                    skippeableRunnerInstance.SkipReason = $"Skipped by the Intelligent Test Runner";
                }

                // Skip test support
                if (runnerInstance.SkipReason != null)
                {
                    XUnitIntegration.CreateScope(ref runnerInstance, instance.GetType());
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
