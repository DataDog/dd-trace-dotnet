// <copyright file="NUnitWorkItemPerformWorkIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Serilog.Events;

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
    MaximumVersion = "4.*.*",
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
        where TTarget : IWorkItem, IDuckType
    {
        if (!NUnitIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        WriteDebugInfo(instance);
        var item = instance.Test;

        switch (item.TestType)
        {
            case "Assembly" when NUnitIntegration.GetTestModuleFrom(item) is null && item.Instance.TryDuckCast<TestAssemblyStruct>(out var itemAssembly):
                var assemblyName = itemAssembly.Assembly?.GetName().Name ?? string.Empty;
                var frameworkVersion = item.Type.Assembly.GetName().Version?.ToString() ?? string.Empty;
                CIVisibility.WaitForSkippableTaskToFinish();
                var newModule = TestModule.InternalCreate(assemblyName, CommonTags.TestingFrameworkNameNUnit, frameworkVersion);
                newModule.EnableIpcClient();
                NUnitIntegration.SetTestModuleTo(item, newModule);
                break;
            case "TestFixture" when NUnitIntegration.GetTestSuiteFrom(item) is null && NUnitIntegration.GetTestModuleFrom(item) is { } module:
                NUnitIntegration.SetTestSuiteTo(item, module.InternalGetOrCreateSuite(item.FullName));
                break;
            case "TestMethod":
                if (NUnitIntegration.ShouldSkip(item, out _, out _))
                {
                    var testMethod = item.Method.MethodInfo;
                    Common.Log.Debug("ITR: Test skipped: {Class}.{Name}", testMethod.DeclaringType?.FullName, testMethod.Name);
                    item.RunState = RunState.Ignored;
                    item.Properties.Set(NUnitIntegration.SkipReasonKey, IntelligentTestRunnerTags.SkippedByReason);
                }

                break;
        }

        return CallTargetState.GetDefault();
    }

    private static void WriteDebugInfo(IWorkItem workItem)
    {
        if (!Common.Log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        var item = workItem.Test;
        if (item.TestType is "Assembly" or "TestFixture" or "TestMethod")
        {
            var strMessage = $"->{workItem.Type.Name}.PerformWork | TestType: {item.TestType} | FullName: {item.FullName} | MethodName: {item.MethodName} | Id: {item.Id} | IsSuite: {item.IsSuite} | HasChildren: {item.HasChildren} | Parent FullName: {item.Parent?.FullName}";
            if (item.TestType == "TestFixture")
            {
                strMessage = "\t" + strMessage;
            }
            else if (item.TestType == "TestMethod")
            {
                strMessage = "\t\t" + strMessage;
            }

#pragma warning disable DDLOG004
            Common.Log.Debug(strMessage);
#pragma warning restore DDLOG004
        }
    }
}
