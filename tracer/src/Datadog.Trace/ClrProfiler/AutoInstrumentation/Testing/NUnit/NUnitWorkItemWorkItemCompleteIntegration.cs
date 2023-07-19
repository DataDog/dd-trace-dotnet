// <copyright file="NUnitWorkItemWorkItemCompleteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// NUnit.Framework.Internal.Execution.WorkItem.WorkItemComplete() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Execution.WorkItem",
    MethodName = "WorkItemComplete",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NUnitWorkItemWorkItemCompleteIntegration
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
        var item = instance.Test;
        switch (item.TestType)
        {
            case "Assembly" when NUnitIntegration.GetTestModuleFrom(item) is { } module:
                module.Close();
                break;
            case "TestFixture" when NUnitIntegration.GetTestSuiteFrom(item) is { } suite:
                suite.Close();
                break;
            case "TestMethod" when instance.Result.ResultState.Status == TestStatus.Failed && item.Method?.MethodInfo is not null:
                if (NUnitIntegration.CreateTest(item) is { } test)
                {
                    test.SetErrorInfo("Exception", instance.Result.Message, instance.Result.StackTrace);
                    test.Close(Ci.TestStatus.Fail, TimeSpan.Zero);
                }

                break;
        }

        return CallTargetState.GetDefault();
    }
}
