// <copyright file="XunitTestMethodRunnerContextCtorV3Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
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
    ParameterTypeNames = ["_", "_", "_", "_", "_", "_", "_"],
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XunitTestMethodRunnerContextCtorV3Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TIXunitTestMethod, TIReadOnlyCollection, TExplicitOption, TIMessageBus, TExceptionAggregator>(
        TTarget instance,
        TIXunitTestMethod testMethod,
        TIReadOnlyCollection testCases,
        TExplicitOption explicitOption,
        ref TIMessageBus messageBus,
        TExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        object?[] constructorArguments)
        where TIXunitTestMethod : IXunitTestMethodV3
    {
        Common.Log.Warning("XunitTestMethodRunnerContextCtorV3Integration.OnMethodBegin, instance: {0}, messageBus: {1}, testMethod: {2}", instance, messageBus, testMethod);

        /*
        if (CIVisibility.Settings.EarlyFlakeDetectionEnabled != true &&
            CIVisibility.Settings.FlakyRetryEnabled != true)
        {
            return CallTargetState.GetDefault();
        }
        */

        if (messageBus is null || messageBus is IDuckType)
        {
            Common.Log.Warning("XunitTestMethodRunnerContextCtorV3Integration.OnMethodBegin, messageBus is IDuckType");
            return CallTargetState.GetDefault();
        }

        Common.Log.Warning("XunitTestMethodRunnerContextCtorV3Integration.OnMethodBegin, messageBus is not IDuckType");

        // Let's replace the IMessageBus with our own implementation to process all results before sending them to the original bus
        Common.Log.Debug("EFD/Retry: Current message bus is not a duck type, creating new RetryMessageBus");
        var duckMessageBus = messageBus.DuckCast<IMessageBus>();
        var messageBusInterfaceType = messageBus.GetType().GetInterface("IMessageBus")!;
        var retryMessageBus = new RetryMessageBus(duckMessageBus, 1, 0);

        // EFD is disabled but FlakeRetry is enabled
        retryMessageBus.FlakyRetryEnabled = CIVisibility.Settings.EarlyFlakeDetectionEnabled != true && CIVisibility.Settings.FlakyRetryEnabled == true;
        messageBus = (TIMessageBus)retryMessageBus.DuckImplement(messageBusInterfaceType);

        return CallTargetState.GetDefault();
    }
}
