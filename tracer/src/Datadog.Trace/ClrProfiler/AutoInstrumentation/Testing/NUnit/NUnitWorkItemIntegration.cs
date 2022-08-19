// <copyright file="NUnitWorkItemIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// NUnit.Framework.Internal.Execution.WorkItem.PerformWork() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "nunit.framework",
        TypeName = "NUnit.Framework.Internal.Execution.WorkItem",
        MethodName = "PerformWork",
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = NUnitIntegration.IntegrationName,
        CallTargetIntegrationType = IntegrationType.Derived)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class NUnitWorkItemIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : IWorkItem
        {
            if (!NUnitIntegration.IsEnabled)
            {
                return CallTargetState.GetDefault();
            }

            // Check if the test should be skipped by the ITR
            if (instance.Test is { IsSuite: false, Method.MethodInfo: { } } currentTest && NUnitIntegration.ShouldSkip(currentTest))
            {
                var testMethod = currentTest.Method.MethodInfo;
                Common.Log.Debug("ITR: Test skipped: {class}.{name}", testMethod.DeclaringType?.FullName, testMethod.Name);
                currentTest.RunState = RunState.Ignored;
                currentTest.Properties.Set(NUnitIntegration.SkipReasonKey, "Skipped by the Intelligent Test Runner");
            }

            return CallTargetState.GetDefault();
        }
    }
}
