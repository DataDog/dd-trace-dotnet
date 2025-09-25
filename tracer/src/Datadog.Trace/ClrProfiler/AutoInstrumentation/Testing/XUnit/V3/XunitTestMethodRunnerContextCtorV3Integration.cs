// <copyright file="XunitTestMethodRunnerContextCtorV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Xunit.v3.TestCaseRunner`3.RunTest calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "xunit.v3.core",
    TypeName = "Xunit.v3.XunitTestMethodRunnerContext",
    MethodName = ".ctor",
#pragma warning disable SA1118 // The parameter span multiple lines
    ParameterTypeNames =
    [
        "Xunit.v3.IXunitTestMethod",
        "System.Collections.Generic.IReadOnlyCollection`1[Xunit.v3.IXunitTestCase]",
        "Xunit.Sdk.ExplicitOption",
        "Xunit.v3.IMessageBus",
        "Xunit.v3.ExceptionAggregator",
        "System.Threading.CancellationTokenSource",
        "System.Object[]",
    ],
#pragma warning restore SA1118
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "1.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XunitTestMethodRunnerContextCtorV3Integration
{
    private static Type? _messageBusInterfaceType;

    internal static CallTargetState OnMethodBegin<TTarget, TIXunitTestMethod, TIReadOnlyCollection, TExplicitOption, TIMessageBus, TExceptionAggregator>(
        TTarget instance,
        TIXunitTestMethod testMethod,
        TIReadOnlyCollection testCases,
        TExplicitOption explicitOption,
        ref TIMessageBus messageBus,
        TExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        object?[] constructorArguments)
    {
        var testOptimization = TestOptimization.Instance;

        var isEarlyFlakeDetectionEnabled = testOptimization.EarlyFlakeDetectionFeature?.Enabled == true;
        var isFlakyRetryEnabled = testOptimization.FlakyRetryFeature?.Enabled == true;
        var isTestManagementEnabled = testOptimization.TestManagementFeature?.Enabled == true;

        // If there's no...
        // - EarlyFlakeDetectionFeature enabled
        // - FlakyRetryFeature enabled
        // - TestManagementFeature enabled
        // then we don't need to handle any retry, so we just skip the retry logic.
        if (!isEarlyFlakeDetectionEnabled && !isFlakyRetryEnabled && !isTestManagementEnabled)
        {
            return CallTargetState.GetDefault();
        }

        // If the message bus is null, or it's a duck type, we don't do anything with it
        if (messageBus is null || messageBus is IDuckType)
        {
            return CallTargetState.GetDefault();
        }

        // Let's replace the IMessageBus with our own implementation to process all results before sending them to the original bus
        Common.Log.Debug("EFD/Retry: Current message bus is not a duck type, creating new RetryMessageBus");
        _messageBusInterfaceType ??= messageBus.GetType().GetInterface("IMessageBus")!;
        var duckMessageBus = messageBus.DuckCast<IMessageBus>();
        var retryMessageBus = new RetryMessageBus(duckMessageBus, 1, 0);
        messageBus = (TIMessageBus)retryMessageBus.DuckImplement(_messageBusInterfaceType);
        return CallTargetState.GetDefault();
    }
}
