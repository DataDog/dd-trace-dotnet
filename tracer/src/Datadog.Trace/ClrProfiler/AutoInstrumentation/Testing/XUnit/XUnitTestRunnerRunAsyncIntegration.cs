// <copyright file="XUnitTestRunnerRunAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Sdk.TestRunner`1.RunAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["xunit.execution.dotnet", "xunit.execution.desktop"],
    TypeName = "Xunit.Sdk.TestRunner`1",
    MethodName = "RunAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Xunit.Sdk.RunSummary]",
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestRunnerRunAsyncIntegration
{
    private static readonly int Retries = 9;
    private static readonly ConditionalWeakTable<object, StrongBox<int>> ExecutionCount = new();

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        if (!XUnitIntegration.IsEnabled || instance is null)
        {
            return CallTargetState.GetDefault();
        }

        var runnerInstance = instance.DuckCast<TestRunnerStruct>();
        ITestRunner? testRunnerInstance = null;

        // Check if the test should be skipped by the ITR
        if (XUnitIntegration.ShouldSkip(ref runnerInstance, out _, out _))
        {
            if (instance.TryDuckCast<ITestRunner>(out testRunnerInstance))
            {
                Common.Log.Debug("ITR: Test skipped: {Class}.{Name}", runnerInstance.TestClass?.FullName ?? string.Empty, runnerInstance.TestMethod?.Name ?? string.Empty);
                // Refresh values after skip reason change, and create Skip by ITR span.
                runnerInstance.SkipReason = IntelligentTestRunnerTags.SkippedByReason;
                testRunnerInstance.SkipReason = runnerInstance.SkipReason;
                XUnitIntegration.CreateTest(ref runnerInstance, instance.GetType(), isRetry: false);
                return CallTargetState.GetDefault();
            }
        }

        if (runnerInstance.SkipReason is not null)
        {
            // Skip test support
            XUnitIntegration.CreateTest(ref runnerInstance, instance.GetType(), isRetry: false);
            return CallTargetState.GetDefault();
        }

        if (CIVisibility.Settings.EarlyFlakeDetectionEnabled != true)
        {
            return CallTargetState.GetDefault();
        }

        var totalExecutionNumber = Retries + 1;

        // Get the current execution number for this TestRunner instance
        Common.Log.Debug("Get the current execution number for this TestRunner instance.");
        if (!ExecutionCount.TryGetValue(instance, out var execCount))
        {
            // Create a new strong box to keep count of the number of execution of the TestRunner instance
            Common.Log.Debug<int>("Create a new strong box to keep count of the number of execution of the TestRunner instance. [Total={TotalNumber}]", totalExecutionNumber);
            execCount = new StrongBox<int>(totalExecutionNumber);
            ExecutionCount.Add(instance!, execCount);
        }

        // Try to ducktype the current instance to ITestClassRunner
        Common.Log.Debug("Let's check if the current message bus is our own implementation.");
        if (!instance.TryDuckCast<ITestRunner>(out testRunnerInstance))
        {
            Common.Log.Error("Current test runner instance cannot be ducktyped.");
            return CallTargetState.GetDefault();
        }

        // Let's check if the current message bus is our own implementation.
        if (testRunnerInstance.MessageBus is { } messageBus and not IDuckType)
        {
            // Let's replace the IMessageBus with our own implementation to process all results before sending them to the original bus
            Common.Log.Debug("Current message bus is not a duck type");
            var duckMessageBus = messageBus.DuckCast<IMessageBus>();
            Common.Log.Debug("Getting the interface");
            var messageBusInterfaceType = messageBus.GetType().GetInterface("IMessageBus")!;
            Common.Log.Debug("Create the new bus");
            var newMessageBus = new RetryMessageBus(duckMessageBus, totalExecutionNumber, execCount);
            Common.Log.Debug("Create the new reverse duck type");
            testRunnerInstance.MessageBus = newMessageBus.DuckImplement(messageBusInterfaceType);
        }

        // Decrement the execution number (the method body will do the execution)
        execCount.Value--;

        return new CallTargetState(null, new TestRunnerState(testRunnerInstance, execCount));
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value (Xunit.Sdk.RunSummary)</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Instance of Xunit.Sdk.RunSummary</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
    {
        if (state.State is TestRunnerState { } testRunnerState)
        {
            Common.Log.Debug("Calculating execution index.");
            var execCount = testRunnerState.ExecutionCount;
            var index = Retries - execCount.Value;

            if (index == 0)
            {
                // Let's make decisions based on the first execution regarding slow tests
                var duration = TraceClock.Instance.UtcNow - testRunnerState.StartTime;
                var slowRetriesSettings = CIVisibility.EarlyFlakeDetectionSettings.SlowTestRetries;
                if (slowRetriesSettings.FiveSeconds.HasValue && duration.TotalSeconds < 5)
                {
                    execCount.Value = slowRetriesSettings.FiveSeconds.Value - 1;
                    Common.Log.Information<int>("Number of retries has been set to {Value} for this test that runs under 5 seconds.", execCount.Value);
                }
                else if (slowRetriesSettings.TenSeconds.HasValue && duration.TotalSeconds < 10)
                {
                    execCount.Value = slowRetriesSettings.TenSeconds.Value - 1;
                    Common.Log.Information<int>("Number of retries has been set to {Value} for this test that runs under 10 seconds.", execCount.Value);
                }
                else if (slowRetriesSettings.ThirtySeconds.HasValue && duration.TotalSeconds < 30)
                {
                    execCount.Value = slowRetriesSettings.ThirtySeconds.Value - 1;
                    Common.Log.Information<int>("Number of retries has been set to {Value} for this test that runs under 30 seconds.", execCount.Value);
                }
                else if (slowRetriesSettings.FiveMinutes.HasValue && duration.TotalMinutes < 5)
                {
                    execCount.Value = slowRetriesSettings.FiveMinutes.Value - 1;
                    Common.Log.Information<int>("Number of retries has been set to {Value} for this test that runs under 5 minutes.", execCount.Value);
                }
                else
                {
                    execCount.Value = 0;
                    Common.Log.Information("Number of retries has been set to 0. Current test duration is {Value}", duration);
                }
            }

            if (execCount.Value > 0)
            {
                var retryNumber = index + 1;
                Common.Log.Debug<int, int>("[Retry {Num}] We need to retry, the current retry value is {Value}", retryNumber, execCount.Value);

                // Set the retry as a continuation of this execution. This will be executing recursively until the execution count is 0/
                Common.Log.Debug<int>("[Retry {Num}] Test class runner is duck casted, running a retry.", retryNumber);
                var innerReturnValue = await ((Task<TReturn>)testRunnerState.TestRunner.RunAsync()).ConfigureAwait(false);
                Common.Log.Debug<int>("[Retry {Num}] Duck casting the inner run summary.", retryNumber);
                var innerRunSummary = innerReturnValue.DuckCast<IRunSummary>()!;
                Common.Log.Debug<int>("[Retry {Num}] Duck casting the run summary.", retryNumber);
                var runSummary = returnValue.DuckCast<IRunSummary>()!;
                Common.Log.Debug<int>("[Retry {Num}] Aggregating.", retryNumber);
                runSummary.Aggregate(innerRunSummary);
            }
            else
            {
                Common.Log.Debug("All retries were executed.");
            }

            if (index == 0)
            {
                Common.Log.Debug("Removing skipped and failed if there's a least 1 success.");
                var runSummary = returnValue.DuckCast<IRunSummary>()!;

                // Let's clear the failed and skipped runs if we have at least one successful run
#pragma warning disable DDLOG004
                Common.Log.Debug($"Summary: {testRunnerState.TestRunner.DisplayName} [Total: {runSummary.Total}, Failed: {runSummary.Failed}, Skipped: {runSummary.Skipped}]");
#pragma warning restore DDLOG004
                var passed = runSummary.Total - runSummary.Skipped - runSummary.Failed;
                if (passed > 0)
                {
                    runSummary.Total = 1;
                    runSummary.Failed = 0;
                    runSummary.Skipped = 0;
                }
                else if (runSummary.Skipped > 0)
                {
                    runSummary.Total = 1;
                    runSummary.Skipped = 1;
                    runSummary.Failed = 0;
                }
                else if (runSummary.Failed > 0)
                {
                    runSummary.Total = 1;
                    runSummary.Skipped = 0;
                    runSummary.Failed = 1;
                }

#pragma warning disable DDLOG004
                Common.Log.Debug($"Returned summary: {testRunnerState.TestRunner.DisplayName} [Total: {runSummary.Total}, Failed: {runSummary.Failed}, Skipped: {runSummary.Skipped}]");
#pragma warning restore DDLOG004
            }
        }

        return returnValue;
    }

    private readonly struct TestRunnerState
    {
        public readonly DateTimeOffset StartTime;
        public readonly ITestRunner TestRunner;
        public readonly StrongBox<int> ExecutionCount;

        public TestRunnerState(ITestRunner testRunner, StrongBox<int> executionCount)
        {
            StartTime = TraceClock.Instance.UtcNow;
            TestRunner = testRunner;
            ExecutionCount = executionCount;
        }
    }
}
