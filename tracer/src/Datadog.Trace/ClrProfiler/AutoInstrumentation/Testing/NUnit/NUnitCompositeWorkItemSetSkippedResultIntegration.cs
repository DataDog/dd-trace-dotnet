// <copyright file="NUnitCompositeWorkItemSetSkippedResultIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// Reports NUnit 5 dependency skips that bypass execution and completion of the child WorkItem.
/// </summary>
[InstrumentMethod(
    AssemblyName = "nunit.framework",
    TypeName = "NUnit.Framework.Internal.Execution.CompositeWorkItem",
    MethodName = "SetChildWorkItemSkippedResult",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["NUnit.Framework.Internal.TestResult", "NUnit.Framework.Interfaces.ResultState", ClrNames.String, ClrNames.String],
    MinimumVersion = "5.0.0",
    MaximumVersion = "5.*.*",
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NUnitCompositeWorkItemSetSkippedResultIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TResult, TResultState>(TTarget instance, TResult result, TResultState resultState, string message, string? stackTrace)
        where TTarget : ICompositeWorkItem, IDuckType
        where TResultState : IResultState, IDuckType
    {
        // Dependency skips use FailureSite.Test. Parent and setup skips use FailureSite.Parent and are handled by the existing completion hook.
        return NUnitIntegration.IsEnabled && resultState.Status == TestStatus.Skipped && resultState.Site == FailureSite.Test
                   ? new CallTargetState(null, result)
                   : CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : ICompositeWorkItem, IDuckType
    {
        if (exception is null && state.State.TryDuckCast<ITestResult>(out var result) &&
            result.Test.Instance.TryDuckCast<ITestWithDependency>(out var dependency) && dependency.DependantTest?.Instance is not null)
        {
            NUnitIntegration.ReportDependencySkip(instance, result);
        }

        return CallTargetReturn.GetDefault();
    }
}
