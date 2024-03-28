// <copyright file="NUnitWorkItemWorkItemCompleteIntegration.cs" company="Datadog">
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
/// NUnit.Framework.Internal.Execution.WorkItem.WorkItemComplete() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Execution.WorkItem",
    MethodName = "WorkItemComplete",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "3.0.0",
    MaximumVersion = "4.*.*",
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
        where TTarget : IWorkItem, IDuckType
    {
        if (!NUnitIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        WriteDebugInfo(instance);
        var item = instance.Test;
        var result = instance.Result;

        NUnitIntegration.GetExceptionAndMessage(result, out var exceptionType, out var resultMessage);
        switch (item.TestType)
        {
            case "Assembly" when NUnitIntegration.GetTestModuleFrom(item) is { } module:
                if (result.ResultState.Status == TestStatus.Failed)
                {
                    module.SetErrorInfo(exceptionType, resultMessage, result.StackTrace);
                    module.Tags.Status = TestTags.StatusFail;
                }
                else if (result.ResultState.Status == TestStatus.Passed && !string.IsNullOrEmpty(resultMessage))
                {
                    module.SetTag(TestTags.Message, resultMessage);
                }

                module.Close();
                // Because we are auto-instrumenting a VSTest testhost process we need to manually call the shutdown process
                CIVisibility.Close();

                break;
            case "TestFixture" when NUnitIntegration.GetTestSuiteFrom(item) is { } suite:
                instance.Instance.TryDuckCast<ICompositeWorkItem>(out var compositeWorkItem);
                if (result.ResultState.Status == TestStatus.Failed)
                {
                    suite.SetErrorInfo(exceptionType, resultMessage, result.StackTrace);
                    suite.Tags.Status = TestTags.StatusFail;

                    // Handle setup errors
                    if (result.ResultState.Site == FailureSite.SetUp && compositeWorkItem is not null)
                    {
                        foreach (var child in compositeWorkItem.Children)
                        {
                            if (child.TryDuckCast<IWorkItem>(out var workItem) &&
                                NUnitIntegration.GetOrCreateTest(workItem.Test) is { IsClosed: false } test)
                            {
                                test.SetErrorInfo(exceptionType, resultMessage, result.StackTrace);
                                test.Close(Ci.TestStatus.Fail);
                            }
                        }
                    }
                }
                else if (result.ResultState.Status == TestStatus.Passed && !string.IsNullOrEmpty(resultMessage))
                {
                    suite.SetTag(TestTags.Message, resultMessage);
                }

                // Handle ignored children in a Theory if the theory has been marked as ignored
                if (compositeWorkItem is not null)
                {
                    foreach (var child in compositeWorkItem.Children)
                    {
                        if (child.TryDuckCast<ICompositeWorkItem>(out var childCompositeWorkItem) &&
                            childCompositeWorkItem.Test.RunState == RunState.Ignored)
                        {
                            if (childCompositeWorkItem.Children is { } childCompositeWorkItemChildren)
                            {
                                foreach (var childWorkItemObject in childCompositeWorkItemChildren)
                                {
                                    if (childWorkItemObject.TryDuckCast<IWorkItem>(out var childWorkItem) &&
                                        childWorkItem.Result.ResultState.Site == FailureSite.Parent)
                                    {
                                        if (NUnitIntegration.GetOrCreateTest(childWorkItem.Test) is { IsClosed: false } test)
                                        {
                                            var skipReason = childWorkItem.Result.Message?.Replace("OneTimeSetUp:", string.Empty).Trim();
                                            test.Close(Ci.TestStatus.Skip, TimeSpan.Zero, skipReason);
                                        }
                                    }
                                }
                            }
                            else if (childCompositeWorkItem.Test.HasChildren &&
                                     childCompositeWorkItem.Test.Tests?.Count > 0)
                            {
                                foreach (var childTestObject in childCompositeWorkItem.Test.Tests)
                                {
                                    if (childTestObject.TryDuckCast<ITest>(out var childTest))
                                    {
                                        if (NUnitIntegration.GetOrCreateTest(childTest) is { IsClosed: false } test)
                                        {
                                            var skipReason = childCompositeWorkItem.Result.Message?.Replace("OneTimeSetUp:", string.Empty).Trim();
                                            test.Close(Ci.TestStatus.Skip, TimeSpan.Zero, skipReason);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                suite.Close();
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

            if (workItem.Result.ResultState.Site == FailureSite.SetUp &&
                workItem.Instance.TryDuckCast<ICompositeWorkItem>(out var compositeWorkItem))
            {
                foreach (var child in compositeWorkItem.Children)
                {
                    strMessage += $"| {child}";
                }
            }

#pragma warning disable DDLOG004
            Common.Log.Debug(strMessage);
#pragma warning restore DDLOG004
        }
    }
}
