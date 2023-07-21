// <copyright file="NUnitWorkItemPerformWorkIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

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
    CallTargetIntegrationKind = CallTargetKind.Derived)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NUnitWorkItemPerformWorkIntegration
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
        if (NUnitIntegration.IsEnabled)
        {
            var item = instance.Test;
            switch (item.TestType)
            {
                case "Assembly" when NUnitIntegration.GetTestModuleFrom(item) is null && item.Instance.TryDuckCast<TestAssemblyStruct>(out var itemAssembly):
                    var assemblyName = itemAssembly.Assembly?.GetName().Name ?? string.Empty;
                    var frameworkVersion = item.Type.Assembly.GetName().Version?.ToString() ?? string.Empty;
                    CIVisibility.WaitForSkippableTaskToFinish();
                    NUnitIntegration.SetTestModuleTo(item, TestModule.Create(assemblyName, "NUnit", frameworkVersion));
                    break;
                case "TestFixture" when NUnitIntegration.GetTestSuiteFrom(item) is null && NUnitIntegration.GetTestModuleFrom(item) is { } module:
                    NUnitIntegration.SetTestSuiteTo(item, module.GetOrCreateSuite(item.FullName));
                    break;
                case "TestMethod" when NUnitIntegration.ShouldSkip(item):
                    var testMethod = item.Method.MethodInfo;
                    Common.Log.Debug("ITR: Test skipped: {Class}.{Name}", testMethod.DeclaringType?.FullName, testMethod.Name);
                    item.RunState = RunState.Ignored;
                    item.Properties.Set(NUnitIntegration.SkipReasonKey, "Skipped by the Intelligent Test Runner");
                    break;
            }
        }

        return CallTargetState.GetDefault();
    }
}
