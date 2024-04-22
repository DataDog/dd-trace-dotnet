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
                XUnitIntegration.CreateTest(ref runnerInstance, instance.GetType());
                return CallTargetState.GetDefault();
            }
        }

        if (runnerInstance.SkipReason is not null)
        {
            // Skip test support
            XUnitIntegration.CreateTest(ref runnerInstance, instance.GetType());
            return CallTargetState.GetDefault();
        }

        if (CIVisibility.Settings.EarlyFlakeDetectionEnabled != true)
        {
            return CallTargetState.GetDefault();
        }

        // Try to ducktype the current instance to ITestClassRunner
        if (!instance.TryDuckCast<ITestRunner>(out testRunnerInstance))
        {
            Common.Log.Error("EFD: Current test runner instance cannot be ducktyped.");
            return CallTargetState.GetDefault();
        }

        // Let's check if the current message bus is our own implementation.
        RetryMessageBus retryMessageBus;
        if (testRunnerInstance.MessageBus is IDuckType { Instance: { } } ducktypedMessageBus)
        {
            Common.Log.Debug("EFD: Current message bus is a duck type, retrieving RetryMessageBus instance");
            retryMessageBus = (RetryMessageBus)ducktypedMessageBus.Instance;
        }
        else if (testRunnerInstance.MessageBus is { } messageBus)
        {
            // Let's replace the IMessageBus with our own implementation to process all results before sending them to the original bus
            Common.Log.Debug("EFD: Current message bus is not a duck type, creating new RetryMessageBus");
            var duckMessageBus = messageBus.DuckCast<IMessageBus>();
            var messageBusInterfaceType = messageBus.GetType().GetInterface("IMessageBus")!;
            retryMessageBus = new RetryMessageBus(duckMessageBus, 1, 1);
            testRunnerInstance.MessageBus = retryMessageBus.DuckImplement(messageBusInterfaceType);
        }
        else
        {
            Common.Log.Error("EFD: Message bus is null.");
            return CallTargetState.GetDefault();
        }

        // Decrement the execution number (the method body will do the execution)
        retryMessageBus.ExecutionNumber--;

        return new CallTargetState(null, new TestRunnerState(testRunnerInstance, retryMessageBus));
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
        if (state.State is TestRunnerState { MessageBus: { } messageBus } testRunnerState)
        {
            if (messageBus is { TestIsNew: true, AbortByThreshold: false })
            {
                var index = messageBus.ExecutionIndex;
                if (index == 0)
                {
                    // Let's make decisions based on the first execution regarding slow tests
                    var duration = TraceClock.Instance.UtcNow - testRunnerState.StartTime;
                    messageBus.TotalExecutions = Common.GetNumberOfExecutionsForDuration(duration);
                    messageBus.ExecutionNumber = messageBus.TotalExecutions - 1;
                }

                if (messageBus.ExecutionNumber > 0)
                {
                    var retryNumber = messageBus.ExecutionIndex + 1;
                    // Set the retry as a continuation of this execution. This will be executing recursively until the execution count is 0/
                    Common.Log.Debug<int, int>("EFD: [Retry {Num}] Test class runner is duck casted, running a retry. [Current retry value is {Value}]", retryNumber, messageBus.ExecutionNumber);
                    var innerReturnValue = await ((Task<TReturn>)testRunnerState.TestRunner.RunAsync()).ConfigureAwait(false);
                    if (innerReturnValue.TryDuckCast<IRunSummary>(out var innerRunSummary) &&
                        returnValue.TryDuckCast<IRunSummary>(out var runSummary))
                    {
                        Common.Log.Debug<int>("EFD: [Retry {Num}] Aggregating results.", retryNumber);
                        runSummary.Aggregate(innerRunSummary);
                    }
                    else
                    {
                        Common.Log.Error<int>("EFD: [Retry {Num}] Unable to duck cast the return value to IRunSummary.", retryNumber);
                    }
                }
                else
                {
                    Common.Log.Debug("EFD: All retries were executed.");
                }

                if (index == 0)
                {
                    messageBus.FlushMessages();

                    if (returnValue.TryDuckCast<IRunSummary>(out var runSummary))
                    {
                        // Let's clear the failed and skipped runs if we have at least one successful run
#pragma warning disable DDLOG004
                        Common.Log.Debug($"EFD: Summary: {testRunnerState.TestRunner.DisplayName} [Total: {runSummary.Total}, Failed: {runSummary.Failed}, Skipped: {runSummary.Skipped}]");
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
                        Common.Log.Debug($"EFD: Returned summary: {testRunnerState.TestRunner.DisplayName} [Total: {runSummary.Total}, Failed: {runSummary.Failed}, Skipped: {runSummary.Skipped}]");
#pragma warning restore DDLOG004
                    }
                    else
                    {
                        Common.Log.Error("EFD: Unable to duck cast the return value to IRunSummary.");
                    }
                }
            }
            else
            {
                messageBus.FlushMessages();
            }
        }

        return returnValue;
    }

    private readonly struct TestRunnerState
    {
        public readonly DateTimeOffset StartTime;
        public readonly ITestRunner TestRunner;
        public readonly RetryMessageBus MessageBus;

        public TestRunnerState(ITestRunner testRunner, RetryMessageBus messageBus)
        {
            StartTime = TraceClock.Instance.UtcNow;
            TestRunner = testRunner;
            MessageBus = messageBus;
        }
    }
}
