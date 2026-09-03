// <copyright file="XunitTestMethodRunnerContextCtorV3V4Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3V4;

/// <summary>
/// Replaces the xUnit v3/v4 method runner message bus when retry features are enabled.
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.CoreTestMethodRunnerContext`2",
    MethodName = ".ctor",
#pragma warning disable SA1118 // Parameter list is clearer when each target type is on a separate line
    ParameterTypeNames =
    [
        "!0",
        "System.Collections.Generic.IReadOnlyCollection`1[!1]",
        "Xunit.Sdk.ExplicitOption",
        "Xunit.v3.IMessageBus",
        "Xunit.v3.ExceptionAggregator",
        "System.Threading.CancellationTokenSource",
        "Xunit.Sdk.ParallelMode",
        "Xunit.v3.ExecutionScheduler",
    ],
#pragma warning restore SA1118
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XunitTestMethodRunnerContextCtorV3V4Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTestMethod, TTestCases, TExplicitOption, TMessageBus, TExceptionAggregator, TParallelMode, TScheduler>(
        TTarget instance,
        TTestMethod testMethod,
        TTestCases testCases,
        TExplicitOption explicitOption,
        ref TMessageBus messageBus,
        TExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        TParallelMode parallelMode,
        TScheduler scheduler)
    {
        var testOptimization = TestOptimization.Instance;
        if (testOptimization.EarlyFlakeDetectionFeature?.Enabled != true &&
            testOptimization.FlakyRetryFeature?.Enabled != true &&
            testOptimization.TestManagementFeature?.Enabled != true)
        {
            return CallTargetState.GetDefault();
        }

        if (messageBus is null || messageBus is IDuckType)
        {
            return CallTargetState.GetDefault();
        }

        var retryMessageBus = new RetryMessageBus(messageBus.DuckCast<IMessageBus>(), 1, 0);
        messageBus = (TMessageBus)retryMessageBus.DuckImplement(typeof(TMessageBus));
        return CallTargetState.GetDefault();
    }
}
